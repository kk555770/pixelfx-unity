using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Pixelfx.Tests
{
    public class ParityTests
    {
        const string Folder = "Packages/com.pixelfx.unity/Tests/Editor/";

        static Dictionary<string, Parity.Case> Reference => Parity.Load(Path.GetFullPath(Folder + "Parity.json"));

        static Dictionary<string, Parity.ActCase> Acts => Parity.LoadActs(Path.GetFullPath(Folder + "ActParity.json"));

        static IEnumerable<string> Ported => Effects.All.Select(m => m.Name);

        static IEnumerable<string> Acted => Effects.All.Where(m => m.Act != null).Select(m => m.Name);

        [TestCaseSource(nameof(Ported))]
        public void EveryFrameMatchesTheJsOriginal(string name) => Assert.IsNull(Parity.Mismatch(Reference[name], Effects.Get(name), null, 0));

        [TestCaseSource(nameof(Acted))]
        public void TheActMatchesTheJsOriginal(string name) => Assert.IsNull(Parity.ActMismatch(Acts[name], Effects.Get(name)));

        [Test]
        public void EveryPortedEffectHasAJsReference() => CollectionAssert.IsSubsetOf(Ported, Reference.Keys);

        [Test]
        public void EveryActHasAJsReference() => CollectionAssert.AreEquivalent(Acted, Acts.Keys.Where(Ported.Contains));
    }
}
