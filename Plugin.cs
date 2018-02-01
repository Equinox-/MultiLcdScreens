using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using Sandbox.Definitions;
using Sandbox.Game.Entities.Blocks;
using Sandbox.ModAPI;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Session;
using Torch.Managers.PatchManager;
using Torch.Utils;
using VRage.FileSystem;
using VRage.Game;
using VRage.Game.Components;
using VRage.Meta;
using VRage.ObjectBuilders;
using VRage.Scripting;
using VRageMath;

namespace RemoteLcd
{
    public class Plugin : TorchPluginBase
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        [ReflectedStaticMethod(Type = typeof(MyComponentTypeFactory), Name = "RegisterFromAssembly")]
        private static readonly Action<Assembly> _registerComponentTypes;

        [ReflectedGetter(Type = typeof(MyComponentFactory), Name = "m_objectFactory")]
        private static readonly Func<MyObjectFactory<MyComponentBuilderAttribute, MyComponentBase>> _componentFactory;

        private static bool _registered;

        private static void CheckRegistration()
        {
            if (_registered)
                return;
            _registered = true;
            var asm = typeof(Plugin).Assembly;
            MyDefinitionManagerBase.RegisterTypesFromAssembly(asm);
            _registerComponentTypes(asm);
            MyObjectBuilderType.RegisterFromAssembly(asm, true);
            _componentFactory().RegisterFromAssembly(asm);


            MyScriptCompiler.Static.AddReferencedAssemblies(typeof(Plugin).Assembly.Location);
            MyScriptCompiler.Static.AddConditionalCompilationSymbols("MULTI_SCREEN");
            MyScriptCompiler.Static.AddImplicitIngameNamespacesFromTypes(typeof(MultiScreenApi));
            using (var whitelist = MyScriptCompiler.Static.Whitelist.OpenBatch())
                whitelist.AllowTypes(MyWhitelistTarget.Both, typeof(MultiScreenApi), typeof(ScreenView));

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
        }

        private static Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var an = new AssemblyName(args.Name);
            return an.Name.Equals(typeof(Plugin).Assembly.GetName().Name) ? typeof(Plugin).Assembly : null;
        }

        /// <inheritdoc />
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Torch.Managers.GetManager<ITorchSessionManager>().AddFactory((x) => new ScreenUpdateComponent(torch));
            ((TorchBase) torch).GameStateChanged += (game, state) =>
            {
                if (state == TorchGameState.Created)
                    CheckRegistration();
            };
        }

        public override void Update()
        {
            ScreenUpdateComponent.Instance?.Update();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
        }
    }
}