using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.EntityComponents.Renders;
using Sandbox.ModAPI;
using Torch.API;
using Torch.Managers;
using Torch.Managers.PatchManager;
using VRage.Game.Components;
using VRage.Game.Definitions;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using VRageRender;
using VRageRender.Messages;

namespace RemoteLcd
{
    public class RemoteLcdManager : Manager
    {
        public static RemoteLcdManager Instance { get; private set; }

        [Dependency]
        private readonly PatchManager _patcher;

        private readonly ConditionalWeakTable<MyTextPanel, PerLcdData> _table =
            new ConditionalWeakTable<MyTextPanel, PerLcdData>();

        private PatchContext _patchContext;

        public RemoteLcdManager(ITorchBase torch) : base(torch)
        {
        }

        private class PerLcdData
        {
            public readonly List<KeyValuePair<IMyEntity, string>> Entities =
                new List<KeyValuePair<IMyEntity, string>>();

            public void Apply(MyTextPanel panel,
                string text, MyFontDefinition font, float scale, Color fontColor, Color backgroundColor,
                int textureResolution, int aspectRatio, TextAlignmentEnum alignment)
            {
                foreach (var kv in Entities)
                {
                    if (kv.Key.Render?.RenderObjectIDs == null)
                        continue;
                    foreach (var rid in kv.Key.Render.RenderObjectIDs)
                    {
                        if (rid == uint.MaxValue)
                            continue;
                        string texId = "LCDRemoteTexture_" + rid + "_" + kv.Value;
                        int width = textureResolution * aspectRatio;
                        MyRenderProxy.CreateGeneratedTexture(texId, width, textureResolution);
                        MyRenderProxy.DrawStringAligned((int) font.Id.SubtypeId, Vector2.Zero, fontColor, text, scale,
                            float.PositiveInfinity, texId, width, (MyRenderTextAlignmentEnum) alignment);
                        MyRenderProxy.RenderOffscreenTextureToMaterial(rid, kv.Value, texId, backgroundColor);
                    }
                }
            }
        }

        public void RaiseApply(MyTextPanel panel, string text, float scale, Color fontColor, Color backgroundColor,
            int textureResolution, int aspectRatio, TextAlignmentEnum alignment)
        {
            if (_table.TryGetValue(panel, out var tmp))
            {
                var font = MyDefinitionManager.Static.GetDefinition<MyFontDefinition>(panel.Font);
                tmp.Apply(panel, text, font, scale, fontColor, backgroundColor, textureResolution, aspectRatio,
                    alignment);
            }
        }

        public override void Attach()
        {
            base.Attach();
            Instance = this;
            _patchContext = _patcher.AcquireContext();
            TextPanelRenderHook.Patch(_patchContext);
            _patcher.Commit();

            MyAPIGateway.Entities.GetEntities(null, (x) =>
            {
                Watch(x);
                return false;
            });
            MyAPIGateway.Entities.OnEntityAdd += Watch;
            MyAPIGateway.Entities.OnEntityRemove += Unwatch;
        }

        private readonly HashSet<IMyEntity> _watched = new HashSet<IMyEntity>();

        private void Watch(IMyEntity x)
        {
            if (_watched.Contains(x))
                return;
            if (x is IMyCubeGrid grid)
            {
                _watched.Add(x);
                grid.OnBlockAdded += Watch;
                grid.OnBlockRemoved += Unwatch;
                grid.GetBlocks(null, (f) =>
                {
                    Watch(f);
                    return false;
                });
            }

            if (x is MyTextPanel panel)
            {
                _watched.Add(x);
                Apply(panel);
            }
        }

        private void Apply(MyTextPanel panel)
        {
            panel.CustomNameChanged += Panel_CustomNameChanged;
            Panel_CustomNameChanged(panel);
        }

        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private void Panel_CustomNameChanged(Sandbox.Game.Entities.Cube.MyTerminalBlock obj)
        {
            if (obj is MyTextPanel text)
            {
                var data = _table.GetOrCreateValue(text);
                var name = obj.CustomName.ToString();
                var si = name.IndexOf("[RLC ", StringComparison.OrdinalIgnoreCase);
                if (si < 0)
                    return;
                var ei = name.IndexOf("]", si, StringComparison.OrdinalIgnoreCase);
                if (ei < 0)
                    return;

                var parts = name.Substring(si + 5, ei - (si + 5)).Split(' ');
                data.Entities.Clear();
                foreach (var kv in parts)
                {
                    var id = kv.Split('/');
                    if (id.Length != 2)
                        continue;
                    MyAPIGateway.Utilities.ShowMessage("RLC Link", id[0] + "\t" + id[1]);
                    var block = FindBlock(obj.CubeGrid, id[0].Trim());
                    if (block != null)
                        data.Entities.Add(new KeyValuePair<IMyEntity, string>(block, id[1].Trim()));
                }
            }
        }

        private IMyEntity FindBlock(IMyCubeGrid grid, string name)
        {
            var tmp = new List<IMySlimBlock>();
            grid.GetBlocks(tmp, (x) => x.FatBlock?.Render != null);
            foreach (var kv in tmp)
                if (kv.FatBlock is IMyTerminalBlock tm &&
                    tm.CustomName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return kv.FatBlock;
            return null;
        }

        private void Revert(MyTextPanel panel)
        {
            panel.CustomNameChanged -= Panel_CustomNameChanged;
        }

        private void Unwatch(IMyEntity x)
        {
            if (!_watched.Remove(x))
                return;
            if (x is IMyCubeGrid grid)
            {
                grid.OnBlockAdded -= Watch;
                grid.OnBlockRemoved -= Unwatch;
                grid.GetBlocks(null, (f) =>
                {
                    Unwatch(f);
                    return false;
                });
            }

            if (x is MyTextPanel panel)
            {
                Revert(panel);
                _table.Remove(panel);
            }
        }

        private void Watch(IMySlimBlock obj)
        {
            if (obj.FatBlock != null)
                Watch(obj.FatBlock);
        }

        private void Unwatch(IMySlimBlock obj)
        {
            if (obj.FatBlock != null)
                Unwatch(obj.FatBlock);
        }

        public override void Detach()
        {
            base.Detach();
            _patcher.FreeContext(_patchContext);
            _patchContext = null;
            Instance = null;
            MyAPIGateway.Entities.GetEntities(null, (x) =>
            {
                Unwatch(x);
                return false;
            });
            MyAPIGateway.Entities.OnEntityAdd -= Watch;
            MyAPIGateway.Entities.OnEntityRemove -= Unwatch;
        }
    }
}