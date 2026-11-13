using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Pixelfx.Tests
{
    public class ParityTests
    {
        static Dictionary<string, Parity.Case> Reference => Parity.Load(Path.GetFullPath("Packages/com.pixelfx.unity/Tests/Editor/Parity.json"));

        static IEnumerable<string> Ported => Effects.All.Select(m => m.Name);

        [TestCaseSource(nameof(Ported))]
        public void EveryFrameMatchesTheJsOriginal(string name) => Assert.IsNull(Parity.Mismatch(Reference[name], Effects.Get(name), null, 0));

        [Test]
        public void EveryPortedEffectHasAJsReference() => CollectionAssert.IsSubsetOf(Ported, Reference.Keys);
    }
}
