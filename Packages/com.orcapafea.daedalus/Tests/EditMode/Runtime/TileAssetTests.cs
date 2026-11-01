using System;
using System.Reflection;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    public class TileAssetTests
    {
        [Test]
        public void KindValuesArePinnedAndThereAreExactlyFour()
        {
            Assert.AreEqual(0, (int)TileCollisionKind.None);
            Assert.AreEqual(1, (int)TileCollisionKind.Solid);
            Assert.AreEqual(2, (int)TileCollisionKind.OneWay);
            Assert.AreEqual(3, (int)TileCollisionKind.Climbable);
            Assert.AreEqual(4, Enum.GetValues(typeof(TileCollisionKind)).Length, "hazard (4) is reserved, not declared");
        }

        [Test]
        public void OneWayIsLabelledOneDashWay()
        {
            var label = typeof(TileCollisionKind).GetField(nameof(TileCollisionKind.OneWay)).GetCustomAttribute<InspectorNameAttribute>();
            Assert.IsNotNull(label);
            Assert.AreEqual("One-way", label.displayName);
        }

        [Test]
        public void ToSimKindKeepsTheNumber()
        {
            foreach (TileCollisionKind kind in Enum.GetValues(typeof(TileCollisionKind)))
            {
                Assert.AreEqual((int)kind, (int)DaedalusTile.ToSimKind(kind), kind.ToString());
            }
            Assert.AreEqual(TileKind.OneWay, DaedalusTile.ToSimKind(TileCollisionKind.OneWay));
            Assert.Throws<ArgumentOutOfRangeException>(() => DaedalusTile.ToSimKind((TileCollisionKind)4));
        }

        [Test]
        public void ANewTileIsSolidAtDataVersionOne()
        {
            var tile = ScriptableObject.CreateInstance<DaedalusTile>();
            try
            {
                var serialized = new SerializedObject(tile);
                SerializedProperty kind = serialized.FindProperty("kind");
                SerializedProperty dataVersion = serialized.FindProperty("dataVersion");
                Assert.IsNotNull(kind, "field 'kind'");
                Assert.IsNotNull(dataVersion, "field 'dataVersion'");
                Assert.AreEqual((int)TileCollisionKind.Solid, kind.intValue);
                Assert.AreEqual(1, dataVersion.intValue);
                Assert.IsNotNull(typeof(DaedalusTile).GetField("dataVersion", BindingFlags.NonPublic | BindingFlags.Instance).GetCustomAttribute<HideInInspector>());
            }
            finally
            {
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void CreateMenuIsDaedalusTile()
        {
            var menu = typeof(DaedalusTile).GetCustomAttribute<CreateAssetMenuAttribute>();
            Assert.IsNotNull(menu);
            Assert.AreEqual("Daedalus/Tile", menu.menuName);
        }
    }
}
