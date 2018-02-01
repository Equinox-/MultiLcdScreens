using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Torch.API;
using Torch.Managers;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.Utils;
using VRageMath;
using VRageRender;
using VRageRender.Messages;

namespace RemoteLcd
{
    public class ScreenUpdateComponent : Manager
    {
        public static ScreenUpdateComponent Instance { get; private set; }

        private readonly HashSet<MultiScreenComponent> _markedForUpdate = new HashSet<MultiScreenComponent>();
        private readonly Queue<MultiScreenComponent> _queuedForUpdate = new Queue<MultiScreenComponent>();


        public void QueueUpdate(MultiScreenComponent component)
        {
            if (_markedForUpdate.Add(component))
                _queuedForUpdate.Enqueue(component);
        }


        [Dependency]
        private readonly PatchManager _patcher;

        private PatchContext _patchContext;

        public ScreenUpdateComponent(ITorchBase torch) : base(torch)
        {
        }


        public override void Attach()
        {
            Instance = this;
            base.Attach();
            _patchContext = _patcher.AcquireContext();
            Patch(_patchContext);
            _patcher.Commit();
        }

        public void Update()
        {
            if (Sandbox.Engine.Platform.Game.IsDedicated)
            {
                _queuedForUpdate.Clear();
                _markedForUpdate.Clear();
                return;
            }

            if (_queuedForUpdate.Count > 0)
            {
                var q = _queuedForUpdate.Dequeue();
                if (q.IsInRange() && q.ProcessOne(int.MaxValue))
                    _queuedForUpdate.Enqueue(q);
                else
                    _markedForUpdate.Remove(q);
            }
        }


        public override void Detach()
        {
            base.Detach();
            _patcher.FreeContext(_patchContext);
            _patchContext = null;
            Instance = null;
        }
#pragma warning disable 649
        [ReflectedMethodInfo(typeof(MySandboxGame), nameof(MySandboxGame.ProcessRenderOutput))]
        private static readonly MethodInfo _renderTextToTextureAligned;

        [ReflectedPropertyInfo(typeof(MyRenderMessageBase), nameof(MyRenderMessageBase.MessageType))]
        private static readonly PropertyInfo _renderMessageType;

        [ReflectedFieldInfo(typeof(MyRenderMessageRenderTextureFreed),
            nameof(MyRenderMessageRenderTextureFreed.FreeResources))]
        private static readonly FieldInfo _renderTextureFreeResources;

        [ReflectedFieldInfo(typeof(MyRenderProxy), nameof(MyRenderProxy.MessagePool))]
        private static readonly FieldInfo _renderMessagePool;

        [ReflectedMethodInfo(typeof(MyMessagePool), nameof(MyMessagePool.Return))]
        private static readonly MethodInfo _renderMessageReturn;
#pragma warning restore 649


        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(_renderTextToTextureAligned).Transpilers.Add(Target(nameof(TranspileProcessRenderOutput)));
        }

        private static MethodInfo Target(string name)
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            return typeof(ScreenUpdateComponent).GetMethod(name, flags);
        }

        private static IEnumerable<MsilInstruction> TranspileProcessRenderOutput(IEnumerable<MsilInstruction> input)
        {
            var stream = input.ToList();
            var fix = new MsilLabel();
            MsilLocal messageLocal = null;

            for (var index = 0; index < stream.Count; index++)
            {
                var i = stream[index];
                yield return i;
                if (i.OpCode == OpCodes.Callvirt && i.Operand is MsilOperandInline<MethodBase> operand &&
                    operand.Value.Name.Equals("TryDequeue") && messageLocal == null)
                {
                    // rewind to find address load
                    for (var j = index - 1; j >= 0; j--)
                        if (stream[j].OpCode == OpCodes.Ldloca || stream[j].OpCode == OpCodes.Ldloca_S)
                        {
                            messageLocal = stream[j].GetReferencedLocal();
                            break;
                        }

                    if (messageLocal != null)
                    {
                        yield return new MsilInstruction(OpCodes.Dup); // duplicate true/false result
                        var endOfJump = new MsilLabel();
                        yield return new MsilInstruction(OpCodes.Brfalse).InlineTarget(endOfJump);
                        yield return messageLocal.AsValueLoad();
                        yield return new MsilInstruction(OpCodes.Callvirt).InlineValue(_renderMessageType.GetMethod);
                        yield return new MsilInstruction(OpCodes.Ldc_I4).InlineValue(
                            (int) MyRenderMessageEnum.RenderTextureFreed);
                        yield return new MsilInstruction(OpCodes.Beq).InlineTarget(fix);

                        yield return new MsilInstruction(OpCodes.Nop).LabelWith(endOfJump);
                    }
                }
            }

            if (messageLocal == null)
                yield break;

            yield return new MsilInstruction(OpCodes.Pop).LabelWith(fix);
            yield return messageLocal.AsValueLoad();
            yield return new MsilInstruction(OpCodes.Castclass).InlineValue(typeof(MyRenderMessageRenderTextureFreed));
            yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(_renderTextureFreeResources);
            yield return new MsilInstruction(OpCodes.Call).InlineValue(Target(nameof(Process)));

            yield return new MsilInstruction(OpCodes.Ldsfld).InlineValue(_renderMessagePool);
            yield return messageLocal.AsValueLoad();
            yield return new MsilInstruction(OpCodes.Callvirt).InlineValue(_renderMessageReturn);

            yield return new MsilInstruction(OpCodes.Ret);
        }

        private static void Process(int freeResources)
        {
            MyCamera mainCamera = MySector.MainCamera;
            if (mainCamera == null)
            {
                return;
            }

            var view = new BoundingSphereD(mainCamera.Position + mainCamera.ForwardVector * 100f, 100.0);
            var entities = MyEntities.GetEntitiesInSphere(ref view);
            object best = null;
            double bestDistance = 200.0;
            foreach (MyEntity e in entities)
            {
                if (e is MyTextPanel tp && tp.FailedToRenderTexture && tp.IsInRange() && tp.ShowTextOnScreen)
                {
                    double dist = Vector3D.Distance(e.PositionComp.GetPosition(), mainCamera.Position);
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        best = tp;
                    }
                }

                var ms = e.Components.Get<MultiScreenComponent>();
                if (ms != null && ms.Dirty && ms.IsInRange())
                {
                    double dist = Vector3D.Distance(e.PositionComp.GetPosition(), mainCamera.Position);
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        best = ms;
                    }
                }
            }

            if (best is MyTextPanel text)
                text.RefreshRenderText(freeResources);
            if (best is MultiScreenComponent comp)
                comp.ProcessOne(freeResources);

            entities.Clear();
        }
    }
}