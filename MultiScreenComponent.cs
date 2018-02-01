using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Definitions;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.Game.Utils;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRageRender;
using VRageRender.Messages;

namespace RemoteLcd
{
    [MyComponentBuilder(typeof(MyObjectBuilder_MultiScreenComponent))]
    [MyComponentType(typeof(MultiScreenComponent))]
    public class MultiScreenComponent : MyEntityComponentBase
    {
        public struct ScreenData
        {
            public string Text;
            public float Scale;
            public MyDefinitionId FontId;
            public Color FontColor, BackgroundColor;
            public TextAlignmentEnum Alignment;
            public bool Dirty;
        }

        public MultiScreenComponentDefinition Definition { get; private set; }
        public ScreenData[] Screens { get; private set; }
        private int _dirtyCount = 0;

        public override void OnAddedToContainer()
        {
            MyLog.Default.WriteLine($"Creating multi screen component for {Entity}");
        }

        public void MarkDirty(int id)
        {
            if (!Screens[id].Dirty)
            {
                if (_dirtyCount == 0 && IsInRange())
                {
                    ScreenUpdateComponent.Instance?.QueueUpdate(this);
                }
                _dirtyCount++;
            }

            Screens[id].Dirty = true;
        }

        public override void Init(MyComponentDefinitionBase defBase)
        {
            base.Init(defBase);
            Definition = (MultiScreenComponentDefinition)defBase;
            MyLog.Default.WriteLine($"Creating multi screen component with definition {defBase}");
            if (Definition != null)
                Screens = new ScreenData[Definition.Screens.Count];
        }

        public override string ComponentTypeDebugString => nameof(MultiScreenComponent);

        private int _dirtyOffset;

        public bool Dirty => _dirtyCount > 0;

        public bool IsInRange()
        {
            if (!Entity.InScene)
                return false;
            MyCamera mainCamera = MySector.MainCamera;
            if (mainCamera == null)
                return false;
            return Vector3D.DistanceSquared(Entity.PositionComp.WorldVolume.Center, mainCamera.Position) <
                   200.0 * 200.0;
        }

        public bool ProcessOne(int freeResources)
        {
            if (Screens == null || Entity.Render?.RenderObjectIDs == null)
                return false;
            for (var i = 0; i < Screens.Length; i++)
            {
                var j = (i + _dirtyOffset) % Screens.Length;
                // no modify
                var data = Screens[j];
                if (data.Dirty)
                {
                    // update
                    var height = Definition.Screens[j].TextureResolution;
                    var width = height * Definition.Screens[j].Aspect;
                    if (width * height * Entity.Render.RenderObjectIDs.Length >= freeResources)
                        continue;

                    foreach (var rid in Entity.Render.RenderObjectIDs)
                    {
                        string srv = "TextOffscreenTexture_" + rid + "x" + j;
                        MyRenderProxy.CreateGeneratedTexture(srv, width, height);
                        MyRenderProxy.DrawStringAligned((int) data.FontId.SubtypeId, Vector2.Zero, data.FontColor,
                            data.Text,
                            data.Scale, float.PositiveInfinity, srv, width, (MyRenderTextAlignmentEnum) data.Alignment);
                        MyRenderProxy.RenderOffscreenTextureToMaterial(rid, Definition.Screens[j].MaterialName, srv,
                            data.BackgroundColor);
                    }

                    Screens[j].Dirty = false;
                    _dirtyCount--;
                    _dirtyOffset = (j + 1) % Screens.Length;
                    return true;
                }
            }

            return false;
        }
    }

    [MyObjectBuilderDefinition()]
    [XmlSerializerAssembly("VRage.Game.XmlSerializers")]
    public class MyObjectBuilder_MultiScreenComponent : MyObjectBuilder_ComponentBase
    {
    }
}