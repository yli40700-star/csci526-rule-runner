using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>
    /// The tile legend under the map lists only the tiles a level has (LevelDef.HasAnyTile), so it never names
    /// something the player cannot see, such as cracked ground in chapter 2.
    /// </summary>
    public class TileLegendTests
    {
        static readonly string Crack = Vocabulary.Crumble.ToString();
        static readonly string Orb = Vocabulary.Orb.ToString();
        static readonly string Spike = Vocabulary.Spike.ToString();

        [Test]
        public void EmptyTileListMatchesNothing()
        {
            var level = new LevelDef { Map = "gpgf" };
            Assert.IsFalse(level.HasAnyTile(""));
            Assert.IsFalse(level.HasAnyTile(null));
        }

        [Test]
        public void EitherFloorCounts()
        {
            var level = new LevelDef { Map = "HggcggoH", Lower = "HfgsgggH" };
            Assert.IsTrue(level.HasAnyTile(Crack), "upper floor");
            Assert.IsTrue(level.HasAnyTile(Spike), "lower floor");
            Assert.IsFalse(level.HasAnyTile(Vocabulary.LowWall.ToString()));
        }

        [Test]
        public void AnyOfSeveralTilesIsEnough()
        {
            // The pit entry is listed for pits and for cracked ground, which leaves a pit behind.
            string pitTiles = Vocabulary.Pit.ToString() + Vocabulary.Crumble;
            Assert.IsTrue(new LevelDef { Map = "gcgf" }.HasAnyTile(pitTiles));
            Assert.IsTrue(new LevelDef { Map = "gpgf" }.HasAnyTile(pitTiles));
            Assert.IsFalse(new LevelDef { Map = "gsgf" }.HasAnyTile(pitTiles));
        }

        [TestCase("1-1", false, false)]
        [TestCase("1-3", false, true)]
        [TestCase("2-1", false, false)]
        [TestCase("3-1", true, true)]
        public void ChapterLevelsListOnlyTheirOwnTiles(string id, bool cracked, bool orb)
        {
            var level = LevelDatabase.Get(id);
            Assert.IsNotNull(level, id);
            Assert.AreEqual(cracked, level.HasAnyTile(Crack), id + " cracked ground");
            Assert.AreEqual(orb, level.HasAnyTile(Orb), id + " Energy orb");
        }
    }
}
