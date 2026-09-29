using System;
using NUnit.Framework;

namespace VRLearn.Tests.EditMode
{
    /// <summary>Text written in the scenario editor (any kanji) must render on the headset.</summary>
    public sealed class FontFallbackTests
    {
        [Test]
        public void StaticJapaneseFontsFallBackToTheDynamicFont()
        {
            var type = Type.GetType("DynamicFontFallback, Assembly-CSharp-Editor");
            Assert.That(type, Is.Not.Null);
            Assert.That((bool)type.GetMethod("IsConfigured").Invoke(null, null), Is.True,
                "Run Tools/VRLearn/Setup Dynamic Font Fallback.");
        }
    }
}
