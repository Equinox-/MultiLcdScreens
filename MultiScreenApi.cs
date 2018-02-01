using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common.ObjectBuilders;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders;
using VRageMath;

namespace RemoteLcd
{
    public static class MultiScreenApi
    {
        public static ScreenView Screen(this IMyEntity entity, string key)
        {
            return new ScreenView(entity, key);
        }
    }

    public struct ScreenView
    {
        private readonly MultiScreenComponent _component;
        private readonly int _id;

        public ScreenView(IMyEntity entity, string key)
        {
            var c = entity?.Components.Get<MultiScreenComponent>();
            if (c == null)
                throw new ArgumentException($"Entity {entity} does not have multiple screens", nameof(entity));

            if (c.Screens == null || c.Definition == null)
                throw new ArgumentException($"Entity {entity} does not have a definition", nameof(entity));

            if (!c.Definition.IndexForId.TryGetValue(key, out int index) || index < 0 || index >= c.Screens.Length)
                throw new ArgumentOutOfRangeException($"Screen {key} doesn't exist", nameof(key));

            _component = c;
            _id = index;
        }

        public string Text
        {
            get => _component.Screens[_id].Text;
            set => CheckAndSet(ref _component.Screens[_id].Text, value);
        }

        public Color Background
        {
            get => _component.Screens[_id].BackgroundColor;
            set => CheckAndSet(ref _component.Screens[_id].BackgroundColor, value);
        }

        public Color Foreground
        {
            get => _component.Screens[_id].FontColor;
            set => CheckAndSet(ref _component.Screens[_id].FontColor, value);
        }

        public float Scale
        {
            get => _component.Screens[_id].Scale;
            set => CheckAndSet(ref _component.Screens[_id].Scale, value);
        }

        public SerializableDefinitionId Font
        {
            get => _component.Screens[_id].FontId;
            set => CheckAndSet(ref _component.Screens[_id].FontId, value);
        }

        public string FontName
        {
            get => Font.SubtypeName;
            set => Font = new SerializableDefinitionId(typeof(MyObjectBuilder_FontDefinition), value);
        }

        public int AlignmentInt
        {
            get => (int) Alignment;
            set => Alignment = (TextAlignmentEnum) value;
        }

        public TextAlignmentEnum Alignment
        {
            get => _component.Screens[_id].Alignment;
            set
            {
                if (_component.Screens[_id].Alignment == value)
                    return;
                _component.Screens[_id].Alignment = value;
                _component.MarkDirty(_id);
            }
        }

        private void CheckAndSet<T>(ref T val, T @new) where T : IEquatable<T>
        {
            if (EqualityComparer<T>.Default.Equals(val, @new))
                return;
            val = @new;
            _component.MarkDirty(_id);
        }
    }
}