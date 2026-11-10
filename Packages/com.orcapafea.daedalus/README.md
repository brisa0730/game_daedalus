# Daedalus Platformer Maker

A Unity 6 package (`com.orcapafea.daedalus`) for building simple 2D platformers in the Unity Editor without code. Students place objects, add Daedalus components from the Inspector and tune their settings. The course starts with troll levels: floors that drop, spikes that pop out and exits that run away.

Requires Unity 6000.0 or newer. The Starter sample needs a **Universal 2D** (URP) project: its Global Light 2D is a URP component, and a Built-in project opens it with a missing script.

**Install steps for students (Korean):** [Documentation~/install-ko.md](Documentation~/install-ko.md)

## What works in 0.2.0

- **Player:** run, jump, double jump, wall slide and climb, ropes, tuned with Inspector settings.
- **Traps:** one Trap component that can vanish, appear, move or fall, set off by a zone or a touch.
- **Checkpoints and Exits**, including an Exit that runs away.
- **Stages:** a game can be several scenes. An Exit's Next stage leads to the next one, with a quick fade; deaths add up across the game, and Share my game builds every stage.
- **Painted tiles** on a Tilemap named Collision: solid, one-way, climbable, hazard, or drawn with no collision.
- **Daedalus → Share my game** makes a zip for the class gallery.
- **The Starter sample:** a short troll level to play, a Trap Demo with every kind of trap, a two-stage example, ready-made traps to copy and one spot left to finish. Its art is plain rectangles and simple shapes.

## What doesn't exist yet

No enemies, no attack or hit points, no play-from-here button, no daedalus window, no default art pack, and custom inspectors only for the Trap, Checkpoint and Exit. Problems with a level appear as warnings in the Console.

## What an update promises

Levels you save in 0.1.0 or later keep opening and playing the same in later 0.x updates. Settings you tuned keep their values, and Starter pieces you placed keep their settings. New features arrive switched off or at values that don't change an existing level. Anything we can't keep, we tell you in the update notes before you update. How movement feels by default may still be tuned.

## License

MIT, for this package folder only: see [LICENSE.md](LICENSE.md).
