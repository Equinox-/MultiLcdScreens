using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using Torch.Utils;
using VRage.Game.Components;
using VRageMath;

namespace RemoteLcd
{
    public static class TextPanelRenderHook
    {
        private const string _renderComponentType = "Sandbox.Game.Components.MyRenderComponentTextPanel, Sandbox.Game";

        [ReflectedMethodInfo(null, "RenderTextToTextureAligned", TypeName = _renderComponentType)]
        private static readonly MethodInfo _renderTextToTextureAligned;

        public static void Patch(PatchContext ctx)
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            ctx.GetPattern(_renderTextToTextureAligned).Suffixes
                .Add(typeof(TextPanelRenderHook).GetMethod(nameof(SuffixRenderTextToTexture), flags));
        }

        public static void SuffixRenderTextToTexture(MyEntityComponentBase __instance, string text, float scale,
            Color fontColor, Color backgroundColor, int textureResolution, int aspectRatio, TextAlignmentEnum alignment)
        {
            var panel = __instance.Entity as MyTextPanel;
            var inst = RemoteLcdManager.Instance;
            if (inst == null || panel == null)
                return;
            inst.RaiseApply(panel, text, scale, fontColor, backgroundColor, textureResolution, aspectRatio, alignment);
        }
    }
}