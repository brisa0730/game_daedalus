using System.Collections.Generic;
using NUnit.Framework;
using Orcapafea.Daedalus.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The Trap's editor actions: fit zone, snap to grid, look sync, the zone following Size, Undo.</summary>
    public class TrapAuthoringTests
    {
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
        public void AddComponentGivesASpriteRendererAndASyncedLook()
        {
            Trap trap = NewTrap(1.5f, 0.5f);
            var renderer = trap.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, "RequireComponent added a SpriteRenderer");
            Assert.AreEqual(SpriteDrawMode.Tiled, renderer.drawMode, "the add hook synced the look");
            Assert.AreEqual(new Vector2(3, 1), renderer.size);
            Assert.IsTrue(renderer.enabled, "a Vanish trap shows");
        }

        [Test]
        public void SyncLookShowsVanishAndHidesAppear()
        {
            Trap trap = NewTrap(1.5f, 0.5f);
            trap.Tile = PlainTile();
            trap.Size = new Vector2Int(4, 2);
            trap.Action = TrapAction.Appear;
            TrapAuthoring.SyncLook(trap);
            var renderer = trap.GetComponent<SpriteRenderer>();
            Assert.IsFalse(renderer.enabled, "an Appear trap is saved hidden");
            Assert.AreSame(((Tile)trap.Tile).sprite, renderer.sprite);
            Assert.AreEqual(new Vector2(4, 2), renderer.size);
            trap.Action = TrapAction.Vanish;
            TrapAuthoring.SyncLook(trap);
            Assert.IsTrue(renderer.enabled);
        }

        [Test]
        public void InPlayAnEditLeavesTheRunningLookAlone()
        {
            // In Play the renderer shows whether the level's trap is present; an Inspector edit must not redraw a
            // vanished floor (code review, Phase 3).
            Trap trap = NewTrap(1.5f, 0.5f);
            trap.Tile = PlainTile();
            var renderer = trap.GetComponent<SpriteRenderer>();
            renderer.enabled = false;
            trap.Size = new Vector2Int(5, 1);
            TrapAuthoring.SyncLook(trap, playing: true);
            Assert.IsFalse(renderer.enabled, "a vanished Vanish trap stays hidden");
            Assert.AreEqual(new Vector2(3, 1), renderer.size, "and keeps its size");
        }

        [Test]
        public void FitZoneAndSnapToGridAreUndoable()
        {
            Trap trap = NewTrap(1.8f, 0.5f);
            Rect fitted = trap.Zone;
            trap.Zone = new Rect(-9, -9, 2, 2);
            Undo.IncrementCurrentGroup();
            TrapAuthoring.FitZone(trap);
            Assert.AreEqual(fitted, trap.Zone);
            Undo.PerformUndo();
            Assert.AreEqual(new Rect(-9, -9, 2, 2), trap.Zone, "undone");

            Assert.IsTrue(TrapLook.IsOffGrid(trap), "precondition");
            Undo.IncrementCurrentGroup();
            TrapAuthoring.SnapToGrid(trap);
            Assert.AreEqual(1.5f, trap.transform.position.x);
            Assert.IsFalse(TrapLook.IsOffGrid(trap));
            Undo.PerformUndo();
            Assert.AreEqual(1.8f, trap.transform.position.x, 1e-5f, "undone");
        }

        [Test]
        public void TheZoneFollowsASizeChangeOnlyWhenItWasTheDefault()
        {
            Trap trap = NewTrap(1.5f, 0.5f);
            var oldSize = trap.Size;
            trap.Size = new Vector2Int(5, 1);
            TrapAuthoring.OnSizeChanged(trap, oldSize);
            Assert.AreEqual(TrapLook.DefaultZoneLocal(new Vector2Int(5, 1)), trap.Zone, "the default zone followed");

            var drawn = new Rect(-6, 0, 2, 2);
            trap.Zone = drawn;
            oldSize = trap.Size;
            trap.Size = new Vector2Int(2, 1);
            TrapAuthoring.OnSizeChanged(trap, oldSize);
            Assert.AreEqual(drawn, trap.Zone, "a drawn zone stays");
        }

        [Test]
        public void FitZoneWorksOnSeveralTraps()
        {
            Trap a = NewTrap(1.5f, 0.5f);
            Trap b = NewTrap(8.5f, 0.5f);
            a.Zone = b.Zone = new Rect(0, 0, 9, 9);
            foreach (Trap trap in new[] { a, b })
            {
                TrapAuthoring.FitZone(trap);
            }
            Assert.AreEqual(TrapLook.DefaultZoneLocal(a.Size), a.Zone);
            Assert.AreEqual(TrapLook.DefaultZoneLocal(b.Size), b.Zone);
        }

        [Test]
        public void ATrapOnAnExitKeepsTheExitsLookWhateverItsAction()
        {
            var go = new GameObject("Exit");
            created.Add(go);
            var renderer = go.AddComponent<SpriteRenderer>();
            Sprite sprite = PlainTile().sprite;
            renderer.sprite = sprite;
            go.AddComponent<Exit>();
            Trap trap = ObjectFactory.AddComponent<Trap>(go);
            Assert.IsTrue(TrapLook.IsOnExit(trap));
            Assert.IsFalse(TrapLook.IsIgnored(trap), "it moves the exit");
            foreach (TrapAction action in new[] { TrapAction.Vanish, TrapAction.Appear, TrapAction.Move, TrapAction.Fall })
            {
                trap.Action = action;
                TrapAuthoring.SyncLook(trap);
                Assert.AreEqual(SpriteDrawMode.Simple, renderer.drawMode, $"{action}: the Exit's renderer is left alone");
                Assert.AreSame(sprite, renderer.sprite, $"{action}: its sprite too");
                Assert.IsTrue(renderer.enabled, $"{action}: and it stays shown");
            }
        }

        [Test]
        public void SyncLookShowsMoveAndFall()
        {
            Trap trap = NewTrap(1.5f, 0.5f);
            trap.Tile = PlainTile();
            var renderer = trap.GetComponent<SpriteRenderer>();
            foreach (TrapAction action in new[] { TrapAction.Move, TrapAction.Fall, TrapAction.Vanish })
            {
                trap.Action = TrapAction.Appear;
                TrapAuthoring.SyncLook(trap);
                Assert.IsFalse(renderer.enabled, "precondition: Appear hides it");
                trap.Action = action;
                TrapAuthoring.SyncLook(trap);
                Assert.IsTrue(renderer.enabled, $"{action} is saved shown (a Phase 3 bug saved Move/Fall hidden)");
            }
        }

        [Test]
        public void FitZoneOnAnExitTrapGrowsTheExitBoxOnEverySide()
        {
            var go = new GameObject("Exit");
            created.Add(go);
            go.transform.position = new Vector3(4, 2, 0);
            go.AddComponent<Exit>();
            Trap trap = ObjectFactory.AddComponent<Trap>(go);
            trap.Zone = Rect.MinMaxRect(0, 0, 1, 1);
            TrapAuthoring.FitZone(trap);
            // No sprite: the exit's box is one tile around its position.
            Assert.AreEqual(Rect.MinMaxRect(-1.5f, -1.5f, 1.5f, 1.5f), trap.Zone);
        }

        [Test]
        public void TheEndHandleRoundsToWholeTilesClampsAndUndoes()
        {
            Trap trap = NewTrap(1.5f, 0.5f);
            trap.Action = TrapAction.Move;
            Vector2Int before = trap.Offset;
            Undo.IncrementCurrentGroup();
            TrapAuthoring.SetEnd(trap, new Vector2(1.5f + 4.4f, 0.5f - 1.6f));
            Assert.AreEqual(new Vector2Int(4, -2), trap.Offset, "rounded to whole tiles");
            Undo.PerformUndo();
            Assert.AreEqual(before, trap.Offset, "undone");
            TrapAuthoring.SetEnd(trap, new Vector2(1.5f + 99f, 0.5f - 99f));
            Assert.AreEqual(new Vector2Int(30, -30), trap.Offset, "clamped to the range");
        }

        Trap NewTrap(float x, float y)
        {
            var go = new GameObject("Trap");
            created.Add(go);
            go.transform.position = new Vector3(x, y, 0);
            return ObjectFactory.AddComponent<Trap>(go);
        }

        Tile PlainTile()
        {
            var texture = new Texture2D(16, 16);
            created.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            created.Add(sprite);
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            created.Add(tile);
            return tile;
        }
    }
}
