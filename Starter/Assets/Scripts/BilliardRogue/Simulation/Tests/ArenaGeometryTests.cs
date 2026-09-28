#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation.Tests
{
    public class ArenaGeometryTests
    {
        static ArenaRules DefaultArena() => new();

        [Test]
        public void TopRowTouchesTheTopWallAndDangerRowSitsOnTheLaunchZone()
        {
            var a = DefaultArena();
            var top = ArenaGeometry.CellRect(a, 0, 0);
            Assert.AreEqual(ArenaGeometry.TopWallY(a), top.yMax, 1e-5f);
            var danger = ArenaGeometry.CellRect(a, 3, ArenaGeometry.DangerRow(a));
            Assert.AreEqual(a.launchZoneHeight, danger.yMin, 1e-5f);
            Assert.AreEqual(3f, danger.xMin, 1e-5f);
        }

        [Test]
        public void CellCenterAndRowLookupsAgree()
        {
            var a = DefaultArena();
            for (var row = 0; row < a.rows; row++)
            {
                for (var col = 0; col < a.columns; col++)
                {
                    var c = ArenaGeometry.CellCenter(a, col, row);
                    Assert.AreEqual(row, ArenaGeometry.RowAtY(a, c.y));
                    Assert.AreEqual(col, ArenaGeometry.ColAtX(a, c.x));
                }
            }
            Assert.AreEqual(-1, ArenaGeometry.RowAtY(a, a.launchY));
        }

        [Test]
        public void BossFootprintIsInsetOnEverySide()
        {
            var a = DefaultArena();
            var r = ArenaGeometry.FootprintRect(a, 2, 0, 2, 2, a.bossInset);
            Assert.AreEqual(2f + a.bossInset, r.xMin, 1e-5f);
            Assert.AreEqual(4f - a.bossInset, r.xMax, 1e-5f);
            Assert.AreEqual(ArenaGeometry.TopWallY(a) - a.bossInset, r.yMax, 1e-5f);
            Assert.AreEqual(ArenaGeometry.TopWallY(a) - 2f + a.bossInset, r.yMin, 1e-5f);
        }

        [Test]
        public void LaunchOriginAndAimClampStayInsideTheArena()
        {
            var a = DefaultArena();
            Assert.AreEqual(a.ballRadius, ArenaGeometry.LaunchOrigin(a, 0f).x, 1e-5f);
            Assert.AreEqual(a.columns - a.ballRadius, ArenaGeometry.LaunchOrigin(a, 1f).x, 1e-5f);
            var flat = ArenaGeometry.ClampAim(a, Vector2.right);
            Assert.AreEqual(a.minAimAngleDeg, Mathf.Atan2(flat.y, flat.x) * Mathf.Rad2Deg, 1e-3f);
            var down = ArenaGeometry.ClampAim(a, new Vector2(-1f, -1f));
            Assert.AreEqual(180f - a.minAimAngleDeg, Mathf.Atan2(down.y, down.x) * Mathf.Rad2Deg, 1e-3f);
        }
    }
}
