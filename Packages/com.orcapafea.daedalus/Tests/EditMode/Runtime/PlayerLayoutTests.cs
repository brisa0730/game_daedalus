using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;
using RangeAttribute = UnityEngine.RangeAttribute;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The Player's serialized layout: the names students' scenes keep, defaults from the sim, tooltips and auto-fill.</summary>
    public class PlayerLayoutTests
    {
        const BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly string[] VisiblePaths =
        {
            "m_Script", "collisionTilemap",
            "movement", "movement.runSpeed", "movement.timeToFullSpeed", "movement.jumpHeight", "movement.gravity", "movement.maxFallSpeed",
            "abilities", "abilities.doubleJump", "abilities.wallJump", "abilities.wallClimb", "abilities.ropeClimb",
            "abilities.airJumps", "abilities.wallSlideSpeed", "abilities.climbSpeed",
        };

        // Field → (default, min, max) from the sim.
        static readonly Dictionary<string, float[]> SimValues = new Dictionary<string, float[]>
        {
            { "runSpeed", new[] { MovementSettings.DefaultRunSpeed, MovementSettings.MinRunSpeed, MovementSettings.MaxRunSpeed } },
            { "timeToFullSpeed", new[] { MovementSettings.DefaultTimeToFullSpeed, MovementSettings.MinTimeToFullSpeed, MovementSettings.MaxTimeToFullSpeed } },
            { "jumpHeight", new[] { MovementSettings.DefaultJumpHeight, MovementSettings.MinJumpHeight, MovementSettings.MaxJumpHeight } },
            { "gravity", new[] { MovementSettings.DefaultGravity, MovementSettings.MinGravity, MovementSettings.MaxGravity } },
            { "maxFallSpeed", new[] { MovementSettings.DefaultMaxFallSpeed, MovementSettings.MinMaxFallSpeed, MovementSettings.MaxMaxFallSpeed } },
            { "airJumps", new float[] { MovementSettings.DefaultAirJumps, MovementSettings.MinAirJumps, MovementSettings.MaxAirJumps } },
            { "wallSlideSpeed", new[] { MovementSettings.DefaultWallSlideSpeed, MovementSettings.MinWallSlideSpeed, MovementSettings.MaxWallSlideSpeed } },
            { "climbSpeed", new[] { MovementSettings.DefaultClimbSpeed, MovementSettings.MinClimbSpeed, MovementSettings.MaxClimbSpeed } },
        };

        readonly List<Object> created = new List<Object>();

        [SetUp]
        public void NewScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void DestroyCreated()
        {
            foreach (Object item in created)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }
            created.Clear();
        }

        [Test]
        public void SerializedPathsAreExactlyTheseAndDataVersionIsHidden()
        {
            Player player = NewObject("Player").AddComponent<Player>();
            var serialized = new SerializedObject(player);
            var visible = new List<string>();
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.NextVisible(true))
            {
                visible.Add(iterator.propertyPath);
            }
            CollectionAssert.AreEquivalent(VisiblePaths, visible);

            SerializedProperty dataVersion = serialized.FindProperty("dataVersion");
            Assert.IsNotNull(dataVersion, "field 'dataVersion'");
            Assert.AreEqual(1, dataVersion.intValue);
            Assert.IsNotNull(typeof(Player).GetField("dataVersion", Fields).GetCustomAttribute<HideInInspector>());
        }

        [Test]
        public void SectionsHoldFiveAndSevenSettings()
        {
            Assert.AreEqual(5, SerializedFields(typeof(PlayerMovementSection)).Count);
            Assert.AreEqual(7, SerializedFields(typeof(PlayerAbilitiesSection)).Count);
        }

        [Test]
        public void EveryNumberStartsAtTheSimDefaultWithTheSimRange()
        {
            int checkedFields = 0;
            foreach (object section in new object[] { new PlayerMovementSection(), new PlayerAbilitiesSection() })
            {
                foreach (FieldInfo field in SerializedFields(section.GetType()))
                {
                    if (field.FieldType == typeof(bool))
                    {
                        Assert.AreEqual(false, field.GetValue(section), $"{field.Name} is off by default");
                        continue;
                    }
                    Assert.IsTrue(SimValues.TryGetValue(field.Name, out float[] sim), $"{field.Name} has no sim value");
                    Assert.AreEqual(sim[0], Convert.ToSingle(field.GetValue(section)), $"{field.Name} default");
                    var range = field.GetCustomAttribute<RangeAttribute>();
                    Assert.IsNotNull(range, $"{field.Name} has a slider");
                    Assert.AreEqual(sim[1], range.min, $"{field.Name} min");
                    Assert.AreEqual(sim[2], range.max, $"{field.Name} max");
                    checkedFields++;
                }
            }
            Assert.AreEqual(SimValues.Count, checkedFields, "every number was checked");
        }

        [Test]
        public void EveryVisibleSettingHasATooltip()
        {
            var fields = new List<FieldInfo> { typeof(Player).GetField("collisionTilemap", Fields) };
            fields.AddRange(SerializedFields(typeof(PlayerMovementSection)));
            fields.AddRange(SerializedFields(typeof(PlayerAbilitiesSection)));
            Assert.AreEqual(13, fields.Count);
            foreach (FieldInfo field in fields)
            {
                var tooltip = field.GetCustomAttribute<TooltipAttribute>();
                Assert.IsNotNull(tooltip, field.Name);
                Assert.IsNotEmpty(tooltip.tooltip, field.Name);
            }

            Assert.AreEqual("1 = normal. Higher = snappier jumps and falls. Jump height stays the same.", Tooltip(typeof(PlayerMovementSection), "gravity"));
            StringAssert.Contains("Used only when Double Jump is on.", Tooltip(typeof(PlayerAbilitiesSection), "airJumps"));
            StringAssert.Contains("Used only when Wall Jump is on.", Tooltip(typeof(PlayerAbilitiesSection), "wallSlideSpeed"));
            StringAssert.Contains("Used only when Wall Climb or Rope Climb is on.", Tooltip(typeof(PlayerAbilitiesSection), "climbSpeed"));
            foreach (string toggle in new[] { "doubleJump", "wallJump", "wallClimb", "ropeClimb" })
            {
                // Toggles apply live in Play mode (operator, 2026-10-26); the tooltip says what the hero starts with.
                StringAssert.StartsWith("On from the start: ", Tooltip(typeof(PlayerAbilitiesSection), toggle), toggle);
                StringAssert.DoesNotContain("next time you press Play", Tooltip(typeof(PlayerAbilitiesSection), toggle), toggle);
            }
        }

        [Test]
        public void AddComponentMenuIsDaedalusPlayer()
        {
            var menu = typeof(Player).GetCustomAttribute<AddComponentMenu>();
            Assert.IsNotNull(menu);
            Assert.AreEqual("Daedalus/Player", menu.componentMenu);
        }

        [Test]
        public void AbilitiesAlwaysIncludeJumpAndEachToggleItsFlag()
        {
            var section = new PlayerAbilitiesSection();
            Assert.AreEqual(Abilities.Jump, section.ToAbilities());
            section.DoubleJump = true;
            Assert.AreEqual(Abilities.Jump | Abilities.DoubleJump, section.ToAbilities());
            section.DoubleJump = false;
            section.WallJump = true;
            Assert.AreEqual(Abilities.Jump | Abilities.WallJump, section.ToAbilities());
            section.WallJump = false;
            section.WallClimb = true;
            Assert.AreEqual(Abilities.Jump | Abilities.WallClimb, section.ToAbilities());
            section.WallClimb = false;
            section.RopeClimb = true;
            Assert.AreEqual(Abilities.Jump | Abilities.RopeClimb, section.ToAbilities());
        }

        [Test]
        public void BuildSettingsCopiesEveryNumber()
        {
            Player player = NewObject("Player").AddComponent<Player>();
            player.Movement.RunSpeed = 7;
            player.Movement.TimeToFullSpeed = 0.2f;
            player.Movement.JumpHeight = 4;
            player.Movement.Gravity = 1.5f;
            player.Movement.MaxFallSpeed = 15;
            player.Abilities.AirJumps = 2;
            player.Abilities.WallSlideSpeed = 3;
            player.Abilities.ClimbSpeed = 5;
            MovementSettings s = player.BuildSettings();
            Assert.AreEqual(7f, s.RunSpeed);
            Assert.AreEqual(0.2f, s.TimeToFullSpeed);
            Assert.AreEqual(4f, s.JumpHeight);
            Assert.AreEqual(1.5f, s.Gravity);
            Assert.AreEqual(15f, s.MaxFallSpeed);
            Assert.AreEqual(2, s.AirJumps);
            Assert.AreEqual(3f, s.WallSlideSpeed);
            Assert.AreEqual(5f, s.ClimbSpeed);
        }

        [Test]
        public void AddComponentFillsTheTilemapNamedCollision()
        {
            NewTilemap("Background");
            Tilemap collision = NewTilemap("Collision");
            NewTilemap("Foreground");
            Assert.AreSame(collision, AddPlayer().CollisionTilemap);
        }

        [Test]
        public void AddComponentFillsTheOnlyTilemap()
        {
            Tilemap only = NewTilemap("Tilemap");
            Assert.AreSame(only, AddPlayer().CollisionTilemap);
        }

        [Test]
        public void AddComponentLeavesTheFieldEmptyWithTwoUnnamedTilemaps()
        {
            NewTilemap("Tilemap");
            NewTilemap("Tilemap (1)");
            Assert.IsNull(AddPlayer().CollisionTilemap);
        }

        [Test]
        public void AddComponentLeavesTheFieldEmptyWithNoTilemap()
        {
            Assert.IsNull(AddPlayer().CollisionTilemap);
        }

        Player AddPlayer()
        {
            // The Inspector's Add Component path.
            return ObjectFactory.AddComponent<Player>(NewObject("Player"));
        }

        Tilemap NewTilemap(string name)
        {
            Grid grid = NewObject("Grid").AddComponent<Grid>();
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(grid.transform);
            return tilemapObject.AddComponent<Tilemap>();
        }

        GameObject NewObject(string name)
        {
            var gameObject = new GameObject(name);
            created.Add(gameObject);
            return gameObject;
        }

        static List<FieldInfo> SerializedFields(Type type)
        {
            var fields = new List<FieldInfo>();
            foreach (FieldInfo field in type.GetFields(Fields))
            {
                if (field.GetCustomAttribute<SerializeField>() != null)
                {
                    fields.Add(field);
                }
            }
            return fields;
        }

        static string Tooltip(Type type, string field)
        {
            return type.GetField(field, Fields).GetCustomAttribute<TooltipAttribute>().tooltip;
        }
    }
}
