using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VRage.Game;
using VRage.Game.Definitions;
using VRage.ObjectBuilders;

namespace RemoteLcd
{
    [MyDefinitionType(typeof(MyObjectBuilder_MultiScreenComponentDefinition), null)]
    public class MultiScreenComponentDefinition : MyComponentDefinitionBase
    {
        public struct Screen
        {
            [XmlAttribute("Id")]
            public string Id;
            [XmlAttribute("Material")]
            public string MaterialName;
            [XmlAttribute("Height")]
            public int TextureResolution;
            [XmlAttribute("Aspect")]
            public int Aspect;
        }

        private readonly Dictionary<string, int> _indexForId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private Screen[] _screens;
        public IReadOnlyDictionary<string, int> IndexForId => _indexForId;
        public IReadOnlyList<Screen> Screens => _screens;

        protected override void Init(MyObjectBuilder_DefinitionBase ob)
        {
            base.Init(ob);
            var def = (MyObjectBuilder_MultiScreenComponentDefinition) ob;
            _screens = def.Screens ?? new Screen[0];
            _indexForId.Clear();
            for (var i = 0; i<_screens.Length; i++)
                _indexForId.Add(_screens[i].Id, i);
        }

        public override MyObjectBuilder_DefinitionBase GetObjectBuilder()
        {
            return new MyObjectBuilder_MultiScreenComponentDefinition()
            {
                Id = Id,
                Screens = _screens ?? new Screen[0]
            };
        }
    }

    [XmlSerializerAssembly("VRage.Game.XmlSerializers")]
    [MyObjectBuilderDefinition]
    public class MyObjectBuilder_MultiScreenComponentDefinition : MyObjectBuilder_ComponentDefinitionBase
    {
        [XmlElement("Screen")]
        public MultiScreenComponentDefinition.Screen[] Screens;
    }
}
