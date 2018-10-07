/*
 * ASCII Pitfall!
 * Copyright (C) 2008 Michael Birken
 * 
 * This file is part of ASCII Pitfall!.
 *
 * ASCII Pitfall! is free software; you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published 
 * by the Free Software Foundation; either version 3 of the License, or
 * (at your option) any later version.
 *
 * ASCII Pitfall! is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using Mischel.ConsoleDotNet;

namespace AsciiPitfall {

  interface IEnemy {
    void Update(Program program);
    void Render(Program program);
  }

  class Crocodile : IEnemy {

    private int x;
    private bool mouthOpen;
    private int delay = 120 * 3;
    private bool eating;

    public Crocodile(int x) {
      this.x = x;
    }

    public void Update(Program program) {
      if (eating) {
        program.harryWalkingOnCrocodile = true;
        program.harryY += 0.1;
        if (program.harryY > 49) {
          eating = false;
          program.LoseLife();
        }
      } else if (!program.harryDying) {
        if (delay > 0) {
          delay--;
        } else {
          delay = 120 * 3;
          mouthOpen = !mouthOpen;
        }

        if (!program.harryDying && !program.harrySwinging) {
          if (program.harryY == 24
              && program.harryX > x - 10
              && program.harryX < x + program.crocodileSprites[0].Width - 8) {
            program.harryWalkingOnCrocodile = true;
            if (mouthOpen && program.harryX <= x + program.crocodileSprites[0].Width - 16) {
              eating = true;
              program.harryDying = true;
              program.harryDyingState = HarryDyingState.Drowning;
            }
          }
        }
      }
    }

    public void Render(Program program) {
      if (eating) {
        program.crocodileSprites[1].Draw((int)(x - program.cameraX), 35, program.hiddenBuffer);
        program.harryStandingSprite.Draw((int)(program.harryX - program.cameraX),
            (int)program.harryY, program.hiddenBuffer, program.harryFacingRight);
        program.crocodileMaskSprite.Draw((int)(x - program.cameraX), 43, program.hiddenBuffer);
      } else {
        program.crocodileSprites[mouthOpen ? 1 : 0].Draw((int)(x - program.cameraX), 35, program.hiddenBuffer);
      }
    }
  }

  class Vine : IEnemy {

    private double anchorX;
    private double angle;
    private double t = Math.PI;
    private double endX;
    private double endY;
    private int harryIndex;
    private bool swinging;

    public Vine(double anchorX) {
      this.anchorX = anchorX;
    }

    public void Update(Program program) {
      t += 0.01;
      angle = (Math.PI / 2.0) + (Math.PI / 4.0) * Math.Sin(t);
      endX = anchorX + 125 * Math.Cos(angle);
      endY = 3 + 39 * Math.Sin(angle);

      int harryXDelta = program.harryFacingRight ? -3 : -13;

      if (swinging && program.harrySwinging) {
        double vx = (endX - anchorX) / 100.0;
        double vy = (endY - 3) / 100.0;
        program.harryX = anchorX + harryIndex * vx + harryXDelta;
        program.harryY = harryIndex * vy;
      } else if (swinging) {
        if (program.harryJumpState == HarryJumpState.NotJumping) {
          swinging = false;
        }
      } else if (program.harryJumpState == HarryJumpState.Jumping) {
        double a = anchorX;
        double b = endX - anchorX;
        double c = 3;
        double d = endY - 3;
        double e = program.lastHarryX + harryXDelta;
        double f = program.harryX - e;
        double g = program.lastHarryY;
        double h = program.harryY - f;
        double denominator = f * d - b * h;
        if (denominator != 0) {
          double v = ((a - e) * h + (g - c) * f) / denominator;
          double u = ((g - c) * b + (a - e) * d) / denominator;
          if (v >= 0 && v <= 1 && u >= 0 && u <= 1) {
            double vx = (endX - anchorX) / 100.0;
            double vy = (endY - 3) / 100.0;
            double x = anchorX;
            double y = 3;
            double minDistance = Double.MaxValue;
            for (int i = 0; i < 100; i++) {
              double dx = x - program.harryX + harryXDelta;
              double dy = y - program.harryY;
              double distance = dx * dx + dy * dy;
              if (distance < minDistance) {
                minDistance = distance;
                harryIndex = i;
              }

              x += vx;
              y += vy;
            }
            program.harrySwinging = true;
            swinging = true;
            program.harryJumpState = HarryJumpState.Landing;
          }
        }
      }
    }

    public void Render(Program program) {
      double vx = (endX - anchorX) / 100.0;
      double vy = (endY - 3) / 100.0;
      double x = anchorX;
      double y = 3;

      for (int i = 0; i < 100; i++) {
        int X = (int)(x - program.cameraX);
        int Y = (int)y;

        if (X >= 0 && X < 160 && Y >= 0 && Y < 50) {
          if ((X & 1) == 0) {
            program.hiddenBuffer[Y, X >> 1].Foreground = ConsoleColor.Green;
          } else {
            program.hiddenBuffer[Y, X >> 1].Background = ConsoleColor.Green;
          }
        }

        x += vx;
        y += vy;
      }

      if (program.harrySwinging) {
        program.harrySwingingSprite.Draw((int)(program.harryX - program.cameraX),
            (int)program.harryY, program.hiddenBuffer, program.harryFacingRight);
      } 
    }
  }

  class TarPit : IEnemy {

    private int x;
    private bool drowningHarry;

    public TarPit(int x) {
      this.x = x;
    }

    public void Update(Program program) {
      if (drowningHarry) {
        program.harryY += 0.1;
        if (program.harryY > 49) {
          drowningHarry = false;
          program.LoseLife();
        }
      } else if (program.harryX > x - 6 && program.harryX < x + program.tarPitSprite.Width - 10
          && program.harryJumpState == HarryJumpState.NotJumping
          && !program.harrySwinging
          && !program.harryDying) {
        program.harryDying = true;
        program.harryDyingState = HarryDyingState.Drowning;
        drowningHarry = true;
        if (program.harryX > x + program.tarPitSprite.Width - 19) {
          program.harryFacingRight = false;
        }
      }
    }

    public void Render(Program program) {
      program.tarPitSprite.Draw((int)(x - program.cameraX), 40, program.hiddenBuffer);
      if (drowningHarry) {
        program.harryStandingSprite.Draw((int)(program.harryX - program.cameraX),
            (int)program.harryY, program.hiddenBuffer, program.harryFacingRight);
        program.tarPitMaskSprite.Draw((int)(x - program.cameraX), 44, program.hiddenBuffer);
      }
    }
  }

  class ShiftingQuickSand : IEnemy {

    enum ShiftingQuickSandState { Open, Closing, Closed, Opening }

    private int x;
    private int spriteIndex;
    private ShiftingQuickSandState state = ShiftingQuickSandState.Open;
    private int delay = 200;
    private bool drowningHarry;

    public ShiftingQuickSand(int x) {
      this.x = x;
    }

    public void Update(Program program) {
      switch (state) {
        case ShiftingQuickSandState.Open:
          if (drowningHarry) {
            program.harryY += 0.1;
            if (program.harryY > 49) {
              drowningHarry = false;
              program.LoseLife();
            }
          } else if (program.harryX > x - 6 && program.harryX < x + program.quickSandSprite.Width - 10
              && program.harryJumpState == HarryJumpState.NotJumping
              && !program.harrySwinging
              && !program.harryDying) {
            program.harryDying = true;
            program.harryDyingState = HarryDyingState.Drowning;
            drowningHarry = true;
            if (program.harryX > x + program.quickSandSprite.Width - 19) {
              program.harryFacingRight = false;
            }
          } else if (delay > 0) {
            delay--;
          } else {
            state = ShiftingQuickSandState.Closing;
            delay = 5;
            spriteIndex = 0;
          }
          break;
        case ShiftingQuickSandState.Opening:
          if (delay > 0) {
            delay--;
          } else {
            delay = 5;
            spriteIndex--;
            if (spriteIndex == -1) {
              state = ShiftingQuickSandState.Open;
              delay = 120;
            }
          }
          break;
        case ShiftingQuickSandState.Closing:
          if (delay > 0) {
            delay--;
          } else {
            delay = 5;
            spriteIndex++;
            if (spriteIndex == 6) {
              state = ShiftingQuickSandState.Closed;
              delay = 200;
            }
          }
          break;
        case ShiftingQuickSandState.Closed:
          if (delay > 0) {
            delay--;
          } else {
            state = ShiftingQuickSandState.Opening;
            delay = 5;
            spriteIndex = 5;
          }
          break;
      }
    }

    public void Render(Program program) {
      switch (state) {
        case ShiftingQuickSandState.Open:
          program.quickSandSprite.Draw((int)(x - program.cameraX), 40, program.hiddenBuffer);
          if (drowningHarry) {
            program.harryStandingSprite.Draw((int)(program.harryX - program.cameraX),
                (int)program.harryY, program.hiddenBuffer, program.harryFacingRight);
            program.quickSandMaskSprite.Draw((int)(x - program.cameraX), 44, program.hiddenBuffer);
          }
          break;
        case ShiftingQuickSandState.Opening:
        case ShiftingQuickSandState.Closing:
          program.shiftingQuickSandSprites[spriteIndex]
              .Draw((int)(x - program.cameraX), 40, program.hiddenBuffer);
          break;
        case ShiftingQuickSandState.Closed:
          program.shiftingQuickSandSprites[6].Draw((int)(x - program.cameraX), 40, program.hiddenBuffer);
          break;
      }
    }
  }

  class ShiftingTarPit : IEnemy {

    enum ShiftingTarPitState { Open, Closing, Closed, Opening }

    private int x;
    private int spriteIndex;
    private ShiftingTarPitState state = ShiftingTarPitState.Open;
    private int delay = 200;
    private bool drowningHarry;

    public ShiftingTarPit(int x) {
      this.x = x;
    }

    public void Update(Program program) {
      switch (state) {
        case ShiftingTarPitState.Open:
          if (drowningHarry) {
            program.harryY += 0.1;
            if (program.harryY > 49) {
              drowningHarry = false;
              program.LoseLife();
            }
          } else if (program.harryX > x - 6 && program.harryX < x + program.tarPitSprite.Width - 10
              && program.harryJumpState == HarryJumpState.NotJumping
              && !program.harrySwinging
              && !program.harryDying) {
            program.harryDying = true;
            program.harryDyingState = HarryDyingState.Drowning;
            drowningHarry = true;
            if (program.harryX > x + program.tarPitSprite.Width - 19) {
              program.harryFacingRight = false;
            }
          } else if (delay > 0) {
            delay--;
          } else {
            state = ShiftingTarPitState.Closing;
            delay = 5;
            spriteIndex = 0;
          }
          break;
        case ShiftingTarPitState.Opening:
          if (delay > 0) {
            delay--;
          } else {
            delay = 5;
            spriteIndex--;
            if (spriteIndex == -1) {
              state = ShiftingTarPitState.Open;
              delay = 120;
            }
          }
          break;
        case ShiftingTarPitState.Closing:
          if (delay > 0) {
            delay--;
          } else {
            delay = 5;
            spriteIndex++;
            if (spriteIndex == 6) {
              state = ShiftingTarPitState.Closed;
              delay = 200;
            }
          }
          break;
        case ShiftingTarPitState.Closed:
          if (delay > 0) {
            delay--;
          } else {
            state = ShiftingTarPitState.Opening;
            delay = 5;
            spriteIndex = 5;
          }
          break;
      }
    }

    public void Render(Program program) {
      switch (state) {
        case ShiftingTarPitState.Open:
          program.tarPitSprite.Draw((int)(x - program.cameraX), 40, program.hiddenBuffer);
          if (drowningHarry) {
            program.harryStandingSprite.Draw((int)(program.harryX - program.cameraX),
                (int)program.harryY, program.hiddenBuffer, program.harryFacingRight);
            program.tarPitMaskSprite.Draw((int)(x - program.cameraX), 44, program.hiddenBuffer);
          }
          break;
        case ShiftingTarPitState.Opening:
        case ShiftingTarPitState.Closing:
          program.shiftingTarPitSprites[spriteIndex]
              .Draw((int)(x - program.cameraX), 40, program.hiddenBuffer);
          break;
        case ShiftingTarPitState.Closed:
          program.shiftingTarPitSprites[6].Draw((int)(x - program.cameraX), 40, program.hiddenBuffer);
          break;
      }
    }
  }

  class QuickSand : IEnemy {

    private int x;
    private bool drowningHarry;

    public QuickSand(int x) {
      this.x = x;
    }

    public void Update(Program program) {
      if (drowningHarry) {
        program.harryY += 0.1;
        if (program.harryY > 49) {
          drowningHarry = false;
          program.LoseLife();
        }
      } else if (program.harryX > x - 6 && program.harryX < x + program.quickSandSprite.Width - 10
          && program.harryJumpState == HarryJumpState.NotJumping
          && !program.harrySwinging
          && !program.harryDying
          && !program.harryWalkingOnCrocodile) {
        program.harryDying = true;
        program.harryDyingState = HarryDyingState.Drowning;
        drowningHarry = true;
        if (program.harryX > x + program.tarPitSprite.Width - 19) {
          program.harryFacingRight = false;
        }
      }
    }

    public void Render(Program program) {
      program.quickSandSprite.Draw((int)(x - program.cameraX), 40, program.hiddenBuffer);
      if (drowningHarry) {
        program.harryStandingSprite.Draw((int)(program.harryX - program.cameraX),
            (int)program.harryY, program.hiddenBuffer, program.harryFacingRight);
        program.quickSandMaskSprite.Draw((int)(x - program.cameraX), 44, program.hiddenBuffer);
      }
    }
  }

  class LadderHole : IEnemy {

    private int x;
    private bool falling;
    private Sprite sprite;

    public LadderHole(int x, Sprite sprite) {
      this.x = x;
      this.sprite = sprite;
    }

    public void Update(Program program) {
      if (falling) {
        program.harryVy += 0.02;
        program.harryY += program.harryVy;
        if (program.harryY > 50) {
          program.underground = true;
          program.harryY = -program.harryStandingSprite.Height;
          program.harryVy = 0;
          program.harryJumpState = HarryJumpState.Jumping;
          int sceneIndex = (int)(program.harryX / 320);
          while(sceneIndex < 0) {
            sceneIndex += 255;
          }
          while(sceneIndex >= 255) {
            sceneIndex -= 255;
          }
          program.undergroundIndex = sceneIndex % 3;
          program.harryX = (program.harryX % 320) + 320 * 85 * 2 + 320 * (sceneIndex / 3);
          falling = false;
          program.harryDying = false;
        }
      } else if (program.harryX > x - 6 && program.harryX < x + program.ladderHoleSprite.Width - 10
          && program.harryY == 24
          && !program.harrySwinging
          && !program.harryDying) {
        program.fallingIntoUnderground = true;
        program.harryDying = true;
        program.harryDyingState = HarryDyingState.Drowning;
        falling = true;
      }
    }

    public void Render(Program program) {
      sprite.Draw((int)(x - program.cameraX), 40, program.hiddenBuffer);
      if (falling) {
        program.harryStandingSprite.Draw((int)(program.harryX - program.cameraX),
            (int)program.harryY, program.hiddenBuffer, program.harryFacingRight);
        program.ladderHoleMaskSprite.Draw((int)(x - program.cameraX), 45, program.hiddenBuffer);
      }
    }
  }

  class Ladder : IEnemy {

    public int x;

    public Ladder(int x) {
      this.x = x;
    }

    public void Update(Program program) {
    }

    public void Render(Program program) {
      program.ladderSprite.Draw((int)(x - program.cameraX), 0, program.hiddenBuffer);
    }
  }

  class Wall : IEnemy {

    public int x;

    public Wall(int x) {
      this.x = x;
    }

    public void Update(Program program) {
      if (program.harryX + 12 >= x && program.harryX < x + 4) {
        program.harryX = x - 12;
      } else if (program.harryX >= x + 4 && program.harryX <= x + 24) {
        program.harryX = x + 24;
      }
    }

    public void Render(Program program) {
      program.wallSprite.Draw((int)(x - program.cameraX), 0, program.hiddenBuffer);
    }
  }

  class TunnelHole : IEnemy {

    private int x;
    private bool falling;

    public TunnelHole(int x) {
      this.x = x;
    }

    public void Update(Program program) {
      if (falling) {
        program.harryVy += 0.02;
        program.harryY += program.harryVy;
        if (program.harryY > 50) {
          program.underground = true;
          program.harryY = -program.harryStandingSprite.Height;
          program.harryVy = 0;
          program.harryJumpState = HarryJumpState.Jumping;
          int sceneIndex = (int)(program.harryX / 320);
          while (sceneIndex < 0) {
            sceneIndex += 255;
          }
          while (sceneIndex >= 255) {
            sceneIndex -= 255;
          }
          program.undergroundIndex = sceneIndex % 3;
          program.harryX = (program.harryX % 320) + 320 * 85 * 2 + 320 * (sceneIndex / 3);
          falling = false;
          program.harryDying = false;
        }
      } else if (program.harryX > x - 6 && program.harryX < x + program.tunnelHoleMaskSprite.Width - 10
         && program.harryY == 24
         && !program.harrySwinging
         && !program.harryDying) {
        program.harryDying = true;
        program.harryDyingState = HarryDyingState.Drowning;
        falling = true;
      }
    }

    public void Render(Program program) {
      program.tunnelHoleSprite.Draw((int)(x - program.cameraX), 40, program.hiddenBuffer);
      if (falling) {
        program.harryStandingSprite.Draw((int)(program.harryX - program.cameraX),
            (int)program.harryY, program.hiddenBuffer, program.harryFacingRight);
        program.tunnelHoleMaskSprite.Draw((int)(x - program.cameraX), 45, program.hiddenBuffer);
      }
    }
  }

  class Scorpion : IEnemy {

    private int iterations;
    private int delay;
    private double locationX;
    private bool fast;
    private int spriteIndex;
    private bool facingRight;
    private double originX;

    public Scorpion(double locationX) {
      this.originX = this.locationX = locationX;
    }

    public void Reset() {
      locationX = originX;
    }

    public void Update(Program program) {
      delay--;
      if (delay <= 0) {
        if (fast) {
          delay = 6;
        } else {
          delay = 13;
        }
        spriteIndex = (spriteIndex == 0) ? 1 : 0;
        iterations--;
        if (iterations <= 0) {
          iterations = 20;
          fast = !fast;
        }
      }

      if (!program.harryDying) {
        if (program.harryX < locationX) {
          if (originX - locationX < 450 && locationX - program.harryX > 2) {
            locationX -= 0.4;
          }
          facingRight = false;
        } else {
          if (locationX - originX < 450 && program.harryX - locationX > 2) {
            locationX += 0.4;
          }
          facingRight = true;
        }

        int sceneIndex = (int)(locationX / 320);
        TunnelScene tunnelScene = program.tunnelScenes[program.undergroundIndex, sceneIndex];
        Wall wall = tunnelScene.wall;
        if (wall != null) {
          if (locationX + 32 >= wall.x && locationX < wall.x + 4) {
            locationX = wall.x - 32;
          } else if (locationX >= wall.x + 4 && locationX <= wall.x + 28) {
            locationX = wall.x + 28;
          }
        }

        bool collided = false;
        if (program.harryClimbing) {
          collided = program.scorpionSprites[spriteIndex].Collide((int)locationX, 31, facingRight,
              program.harryClimbingSprite, (int)program.harryX, (int)program.harryY, 
              program.harryClimbSpriteIndex > 1);
        } else if (program.harryJumpState == HarryJumpState.Jumping) {
          collided = program.scorpionSprites[spriteIndex].Collide((int)locationX, 31, facingRight,
              program.harryRunningSprites[4], (int)program.harryX, (int)program.harryY,
              program.harryFacingRight);
        } else if (program.harryRunning) {
          collided = program.scorpionSprites[spriteIndex].Collide((int)locationX, 31, facingRight,
              program.harryRunningSprites[(int)program.harryRunningIndex],
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        } else {
          collided = program.scorpionSprites[spriteIndex].Collide((int)locationX, 31, facingRight,
              program.harryStandingSprite,
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        }
        if (collided) {
          program.fallingIntoUnderground = false;
          program.harryDying = true;
          program.harryDyingState = HarryDyingState.Blinking;
          program.harryDyingDelay = 90.0;
        }
      }
    }

    public void Render(Program program) {
      int x = (int)(locationX - program.cameraX);
      program.scorpionSprites[spriteIndex].Draw(x, 31, program.hiddenBuffer, facingRight);
    }
  }

  class Cobra : IEnemy {

    private int iterations;
    private int delay;
    private double locationX;
    private bool fast;
    private int spriteIndex;

    public Cobra(double locationX) {
      this.locationX = locationX;
    }

    public void Update(Program program) {
      delay--;
      if (delay <= 0) {
        if (fast) {
          delay = 6;
        } else {
          delay = 13;
        }
        spriteIndex = (spriteIndex == 0) ? 1 : 0;
        iterations--;
        if (iterations <= 0) {
          iterations = 20;
          fast = !fast;
        }
      }

      if (!program.harryDying) {
        bool collided = false;
        if (program.harryJumpState == HarryJumpState.Jumping) {
          collided = program.cobraSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryRunningSprites[4], (int)program.harryX, (int)program.harryY,
              program.harryFacingRight);
        } else if (program.harryRunning) {
          collided = program.cobraSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryRunningSprites[(int)program.harryRunningIndex],
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        } else {
          collided = program.cobraSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryStandingSprite,
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        }
        if (collided) {
          program.harryDying = true;
          program.harryDyingState = HarryDyingState.Blinking;
          program.harryDyingDelay = 90.0;
        }
      }
    }

    public void Render(Program program) {
      int x = (int)(locationX - program.cameraX);
      program.cobraSprites[spriteIndex].Draw(x, 31, program.hiddenBuffer);
    }
  }

  class Gold : IEnemy {

    private int iterations;
    private int delay;
    private double locationX;
    private bool fast;
    private int spriteIndex;
    private bool obtained;

    public Gold(double locationX) {
      this.locationX = locationX;
    }

    public void Update(Program program) {
      if (obtained) {
        return;
      }

      delay--;
      if (delay <= 0) {
        if (fast) {
          delay = 6;
        } else {
          delay = 13;
        }
        spriteIndex = (spriteIndex == 0) ? 1 : 0;
        iterations--;
        if (iterations <= 0) {
          iterations = 20;
          fast = !fast;
        }
      }

      if (!program.harryDying) {
        bool collided = false;
        if (program.harryJumpState == HarryJumpState.Jumping) {
          collided = program.goldSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryRunningSprites[4], (int)program.harryX, (int)program.harryY,
              program.harryFacingRight);
        } else if (program.harryRunning) {
          collided = program.goldSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryRunningSprites[(int)program.harryRunningIndex],
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        } else {
          collided = program.goldSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryStandingSprite,
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        }
        if (collided) {
          obtained = true;
          if (program.gameMode == GameMode.Playing) {
            program.score += 4000;
          }
          program.GotTreasure();
        }
      }
    }

    public void Render(Program program) {
      if (obtained) {
        return;
      }
      int x = (int)(locationX - program.cameraX);
      program.goldSprites[spriteIndex].Draw(x, 31, program.hiddenBuffer);
    }
  }

  class Silver : IEnemy {

    private int iterations;
    private int delay;
    private double locationX;
    private bool fast;
    private int spriteIndex;
    private bool obtained;

    public Silver(double locationX) {
      this.locationX = locationX;
    }

    public void Update(Program program) {
      if (obtained) {
        return;
      }

      delay--;
      if (delay <= 0) {
        if (fast) {
          delay = 6;
        } else {
          delay = 13;
        }
        spriteIndex = (spriteIndex == 0) ? 1 : 0;
        iterations--;
        if (iterations <= 0) {
          iterations = 20;
          fast = !fast;
        }
      }

      if (!program.harryDying) {
        bool collided = false;
        if (program.harryJumpState == HarryJumpState.Jumping) {
          collided = program.silverSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryRunningSprites[4], (int)program.harryX, (int)program.harryY,
              program.harryFacingRight);
        } else if (program.harryRunning) {
          collided = program.silverSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryRunningSprites[(int)program.harryRunningIndex],
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        } else {
          collided = program.silverSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryStandingSprite,
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        }
        if (collided) {
          obtained = true;
          if (program.gameMode == GameMode.Playing) {
            program.score += 3000;
          }
          program.GotTreasure();
        }
      }
    }

    public void Render(Program program) {
      if (obtained) {
        return;
      }
      int x = (int)(locationX - program.cameraX);
      program.silverSprites[spriteIndex].Draw(x, 31, program.hiddenBuffer);
    }
  }

  class Money : IEnemy {

    private double locationX;
    private bool obtained;

    public Money(double locationX) {
      this.locationX = locationX;
    }

    public void Update(Program program) {
      if (obtained) {
        return;
      }
      if (!program.harryDying) {
        bool collided = false;
        if (program.harryJumpState == HarryJumpState.Jumping) {
          collided = program.moneySprite.Collide((int)locationX, 31, true,
              program.harryRunningSprites[4], (int)program.harryX, (int)program.harryY,
              program.harryFacingRight);
        } else if (program.harryRunning) {
          collided = program.moneySprite.Collide((int)locationX, 31, true,
              program.harryRunningSprites[(int)program.harryRunningIndex],
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        } else {
          collided = program.moneySprite.Collide((int)locationX, 31, true,
              program.harryStandingSprite,
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        }
        if (collided) {
          obtained = true;
          if (program.gameMode == GameMode.Playing) {
            program.score += 2000;
          }
          program.GotTreasure();
        }
      }
    }

    public void Render(Program program) {
      if (obtained) {
        return;
      }
      int x = (int)(locationX - program.cameraX);
      program.moneySprite.Draw(x, 31, program.hiddenBuffer);
    }
  }

  class Ring : IEnemy {

    private double locationX;
    private bool obtained;

    public Ring(double locationX) {
      this.locationX = locationX;
    }

    public void Update(Program program) {
      if (obtained) {
        return;
      }
      if (!program.harryDying) {
        bool collided = false;
        if (program.harryJumpState == HarryJumpState.Jumping) {
          collided = program.moneySprite.Collide((int)locationX, 31, true,
              program.harryRunningSprites[4], (int)program.harryX, (int)program.harryY,
              program.harryFacingRight);
        } else if (program.harryRunning) {
          collided = program.moneySprite.Collide((int)locationX, 31, true,
              program.harryRunningSprites[(int)program.harryRunningIndex],
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        } else {
          collided = program.moneySprite.Collide((int)locationX, 31, true,
              program.harryStandingSprite,
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        }
        if (collided) {
          obtained = true;
          if (program.gameMode == GameMode.Playing) {
            program.score += 5000;
          }
          program.GotTreasure();
        }
      }
    }

    public void Render(Program program) {
      if (obtained) {
        return;
      }
      int x = (int)(locationX - program.cameraX);
      program.ringSprite.Draw(x, 31, program.hiddenBuffer);
    }
  }

  class Fire : IEnemy {

    private int iterations;
    private int delay;
    private double locationX;
    private bool fast;
    private int spriteIndex;

    public Fire(double locationX) {
      this.locationX = locationX;
    }

    public void Update(Program program) {
      delay--;
      if (delay <= 0) {
        if (fast) {
          delay = 6;
        } else {
          delay = 13;
        }
        spriteIndex = (spriteIndex == 0) ? 1 : 0;
        iterations--;
        if (iterations <= 0) {
          iterations = 20;
          fast = !fast;
        }
      }

      if (!program.harryDying) {
        bool collided = false;
        if (program.harryJumpState == HarryJumpState.Jumping) {
          collided = program.fireSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryRunningSprites[4], (int)program.harryX, (int)program.harryY,
              program.harryFacingRight);
        } else if (program.harryRunning) {
          collided = program.fireSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryRunningSprites[(int)program.harryRunningIndex],
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        } else {
          collided = program.fireSprites[spriteIndex].Collide((int)locationX, 31, true,
              program.harryStandingSprite,
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        }
        if (collided) {
          program.harryDying = true;
          program.harryDyingState = HarryDyingState.Blinking;
          program.harryDyingDelay = 90.0;
        }
      }
    }

    public void Render(Program program) {
      int x = (int)(locationX - program.cameraX);
      program.fireSprites[spriteIndex].Draw(x, 31, program.hiddenBuffer);
    }
  }

  class StationaryLog : IEnemy {

    private double x;

    public StationaryLog(double locationX) {
      this.x = locationX;
    }

    public void Update(Program program) {
      if (!program.harrySwinging && !program.harryDying) {
        bool collided = false;
        if (program.harryJumpState == HarryJumpState.Jumping) {
          collided = program.logSprites[0].Collide((int)x, 34, true,
              program.harryRunningSprites[4], (int)program.harryX, (int)program.harryY,
              program.harryFacingRight);
        } else if (program.harryRunning) {
          collided = program.logSprites[0].Collide((int)x, 34, true,
              program.harryRunningSprites[(int)program.harryRunningIndex],
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        } else {
          collided = program.logSprites[0].Collide((int)x, 34, true,
              program.harryStandingSprite,
              (int)program.harryX, (int)program.harryY, program.harryFacingRight);
        }
        if (collided) {
          program.remainingTicks -= 25;
          program.harryHittingLog = true;
          if (program.score > 0) {
            program.score--;
          }
        }
      }
    }

    public void Render(Program program) {
      int x = (int)(this.x - program.cameraX);
      program.logSprites[0].Draw(x, 34, program.hiddenBuffer);
    }
  }

  class RollingLog : IEnemy {

    enum RollingLogState { Waiting, Dropping, Rolling };

    private double x;
    private double y;
    private double vy;
    private double sceneX;
    private double spriteIndex;
    private int delay;
    private RollingLogState rollingLogState = RollingLogState.Waiting;
    private bool harryKneeling;

    public RollingLog(double sceneX, int delay) {
      this.sceneX = sceneX;
      this.delay = delay;
    }

    public void Update(Program program) {
      harryKneeling = false;
      switch (rollingLogState) {
        case RollingLogState.Waiting:
          if (--delay <= 0) {
            rollingLogState = RollingLogState.Dropping;
            x = sceneX + 319 - program.logSprites[0].Width;
            y = -program.logSprites[0].Height;
          }
          break;
        case RollingLogState.Dropping: {
          y += vy;
          vy += 0.01;
          if (y >= 34) {
            y = 34;
            rollingLogState = RollingLogState.Rolling;
          }

          if (!program.harrySwinging && !program.harryDying) {
            bool collided = false;
            if (program.harryJumpState == HarryJumpState.Jumping) {
              collided = program.logSprites[(int)spriteIndex].Collide((int)x, (int)y, true,
                  program.harryRunningSprites[4], (int)program.harryX, (int)program.harryY,
                  program.harryFacingRight);
            } else if (program.harryRunning) {
              collided = program.logSprites[(int)spriteIndex].Collide((int)x, (int)y, true,
                  program.harryRunningSprites[(int)program.harryRunningIndex],
                  (int)program.harryX, (int)program.harryY, program.harryFacingRight);
            } else {
              collided = program.logSprites[(int)spriteIndex].Collide((int)x, (int)y, true,
                  program.harryStandingSprite,
                  (int)program.harryX, (int)program.harryY, program.harryFacingRight);
            }
            if (collided) {
              program.remainingTicks -= 25;
              program.harryHittingLog = true;
            }
          }
          break;
        }
        case RollingLogState.Rolling: {
          spriteIndex += 0.08;
          if (spriteIndex >= 2) {
            spriteIndex -= 2;
          }

          x -= 0.4;
          if (x <= sceneX) {
            rollingLogState = RollingLogState.Dropping;
            x = sceneX + 319 - program.logSprites[0].Width;
            y = -program.logSprites[0].Height;
            vy = 0;
          }

          if (!program.harrySwinging && !program.harryDying) {
            bool collided = false;
            if (program.harryJumpState == HarryJumpState.Jumping) {
              collided = program.logSprites[(int)spriteIndex].Collide((int)x, (int)y, true,
                  program.harryRunningSprites[4], (int)program.harryX, (int)program.harryY,
                  program.harryFacingRight);
            } else if (program.harryRunning) {
              collided = program.logSprites[(int)spriteIndex].Collide((int)x, (int)y, true,
                  program.harryRunningSprites[(int)program.harryRunningIndex],
                  (int)program.harryX, (int)program.harryY, program.harryFacingRight);
              if (collided) {
                this.harryKneeling = program.harryKneeling = true;
              }
            } else {
              collided = program.logSprites[(int)spriteIndex].Collide((int)x, (int)y, true,
                  program.harryStandingSprite,
                  (int)program.harryX, (int)program.harryY, program.harryFacingRight);
              if (collided) {
                this.harryKneeling = program.harryKneeling = true;
              }
            }
            if (collided) {
              program.remainingTicks -= 25;
              program.harryHittingLog = true;
              if (program.score > 0) {
                program.score--;
              }
            }
          }
          break;
        }
      }
    }

    public void Render(Program program) {
      if (harryKneeling) {
        program.harryRunningSprites[4].Draw(
            (int)(program.harryX - program.cameraX), (int)program.harryY + 5, 
            program.hiddenBuffer, program.harryFacingRight);
      } 
      if (rollingLogState != RollingLogState.Waiting
          && !(x - sceneX < 20 && ((int)x & 1) == 0)) {
        program.logSprites[(int)spriteIndex].Draw((int)(x - program.cameraX), (int)y, program.hiddenBuffer);
      }
    }
  }
}
