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

  enum EndingState { Init, Message, Credits, Characters, TheEnd };
  enum CharacterState { ScrollIn, Pause, ScrollOut }
  enum GameMode { GameOver, TitleScreen, Playing, Ending };
  enum TreasureType { Money = 0, Silver = 1, Gold = 2, Ring = 3 }
  enum SceneType {
    Hole_1,
    Hole_3,
    TarPitWithVine,
    QuickSandWithVine,
    CrocodilePit,
    CrocodilePitWithVine,
    ShiftingTarPitWithTreasure,
    ShiftingTarPitWithVine,
    ShiftingQuickSand,
  }
  enum WallType { Left = 0, Right = 1, NoWall = 2 }
  enum ObjectType {
    RollingLog = 0,
    RollingLog_2a = 1,
    RollingLog_2b = 2,
    RollingLog_3 = 3,
    Log = 4,
    Log_3 = 5,
    Fire = 6,
    Cobra = 7
  }

  enum HarryJumpState { NotJumping, Jumping, Landing }
  enum HarryDyingState { Drowning, Blinking, Panning, Dropping }

  class Scene {
    public TreasureType treasureType;
    public SceneType sceneType;
    public int backgroundType;
    public WallType wallType;
    public ObjectType objectType;
    public string name;
    public List<IEnemy> enemies = new List<IEnemy>();    
  }

  class TunnelScene {
    public string name;
    public List<IEnemy> enemies = new List<IEnemy>();
    public Wall wall;
    public Ladder ladder;
    public Scorpion scorpion;
  }

  partial class Program {
    public const int FRAMES_PER_SECOND = 120;
    public long FREQUENCY = Stopwatch.Frequency;
    public long TICKS_PER_FRAME = Stopwatch.Frequency / FRAMES_PER_SECOND;
    public Stopwatch stopwatch = new Stopwatch();
    public long nextFrameStart;
    public ConsoleInputEventInfo[] inputEvents = new ConsoleInputEventInfo[16];
    public ConsoleScreenBuffer screenBuffer;
    public ConsoleCharInfo[,] hiddenBuffer;
    public ConsoleInputBuffer inputBuffer;
    public bool leftKeyPressed;
    public bool rightKeyPressed;
    public bool upKeyPressed;
    public bool downKeyPressed;
    public bool jumpKeyPressed;
    public bool enterKeyPressed;
    public Scene[] scenes = new Scene[4 * 255];
    public TunnelScene[,] tunnelScenes = new TunnelScene[3, 4 * 85];
    public double cameraX;
    public int[] leafDepths = new int[8 * 160];
    public int[] leafDepths2 = new int[8 * 160];
    public int[] caveDepths = new int[8 * 160];
    public int[] caveDepths2 = new int[8 * 160];
    public double harryX = 100 + 2 * 320 * 255;
    public double harryY = 24;
    public double lastHarryX = 0;
    public double lastHarryY = 24;
    public bool harryFacingRight = true;
    public bool harryRunning;
    public bool harrySwinging;
    public double harryRunningIndex;
    public HarryJumpState harryJumpState = HarryJumpState.NotJumping;
    public double harryVy;
    public string harryLocation = "Location: 1";
    public int remainingTicks = 20 * 60 * 120;
    public StringBuilder timeSB = new StringBuilder("Time: 20:00");
    public bool harryHittingLog;
    public bool harryKneeling;
    public bool harryDying;
    public HarryDyingState harryDyingState;
    public double harryDyingDelay;
    public int score = 0;
    public int treasures = 32;
    public int lives = 4;
    public bool harryWalkingOnCrocodile;
    public bool underground = false;
    public int undergroundIndex = 0;
    public bool harryClimbing;
    public double harryClimbSpriteIndex;
    public bool fallingIntoUnderground;
    public GameMode gameMode = GameMode.TitleScreen;
    public double pressEnterIndex;
    public int gameOverDelay;
    public EndingState endingState = EndingState.Init;
    public double creditsY;
    public CharacterState characterState;
    public int characterIndex;
    public double characterX;
    public double characterX2;
    public int characterDelay;
    public byte[] demoData = new byte[120 * 5 * 60];
    public int demoDataIndex = 0;

    public string[] characterNames = {
      "Log",
      "Fire",
      "Rattlesnake",
      "Quicksand",
      "Crocodile",
      "Scorpion",
      " Money Bag",
      "Silver Bar",
      "Gold Bar",
      "Diamond Ring",      
    };
    public Sprite[] characterSprites;
    public String[] endingStrings = {
      "Congratulations!",
      "Pitfall Harry successfully collected all 32 treasures.",
    };
    public int endingStringIndex;
    public int endingStringCharIndex;

    public Program() {
      Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AsciiPitfall.demo.dat");
      stream.Read(demoData, 0, demoData.Length);
      stream.Close();

      characterSprites = new Sprite[] {
        logSprites[0],
        fireSprites[1],
        cobraSprites[1],
        quickSandSprite,
        crocodileSprites[1],
        scorpionSprites[0],
        moneySprite,
        silverSprites[0],
        goldSprites[1],
        ringSprite,      
      };

      Console.Title = "ASCII Pitfall!";
      inputBuffer = JConsole.GetInputBuffer();
      screenBuffer = JConsole.GetActiveScreenBuffer();
      screenBuffer.SetWindowSize(80, 50);
      screenBuffer.SetBufferSize(80, 50);
      screenBuffer.CursorVisible = false;
      screenBuffer.Clear();
      hiddenBuffer = new ConsoleCharInfo[50, 80];
      GenerateMap();
      GenerateEnemies();
      GenerateBackground();

      for (int y = 0; y < 50; y++) {
        for (int x = 0; x < 80; x++) {
          hiddenBuffer[y, x].AsciiChar = (byte)0xdd;
        }
      }

      stopwatch.Start();
      nextFrameStart = stopwatch.ElapsedTicks;

      StartGame(false);

      while (true) {
        do {
          UpdateModel();
          nextFrameStart += TICKS_PER_FRAME;
        } while (nextFrameStart < stopwatch.ElapsedTicks);
        RenderFrame();
        long remainingTicks = nextFrameStart - stopwatch.ElapsedTicks;
        if (remainingTicks > 0) {
          Thread.Sleep((int)(1000 * remainingTicks / FREQUENCY));
        }
      }
    }

    public void GotTreasure() {
      if (gameMode == GameMode.Playing) {
        treasures--;
        if (treasures == 0) {
          gameMode = GameMode.Ending;
          endingState = EndingState.Init;
        }
      }
    }

    private void StartGame(bool realStart) {
      leftKeyPressed = false;
      rightKeyPressed = false;
      upKeyPressed = false;
      downKeyPressed = false;
      jumpKeyPressed = false;
      enterKeyPressed = false;
      harryX = 100 + 2 * 320 * 255;
      harryY = 24;
      lastHarryX = 0;
      lastHarryY = 24;
      harryFacingRight = true;
      harryRunning = false;
      harrySwinging = false;
      harryRunningIndex = 0;
      harryJumpState = HarryJumpState.NotJumping;
      harryVy = 0;
      harryLocation = "Location: 1";
      remainingTicks = 20 * 60 * 120;
      harryHittingLog = false;
      harryKneeling = false;
      harryDying = false;
      harryDyingDelay = 0;      
      harryWalkingOnCrocodile = false;
      underground = false;
      undergroundIndex = 0;
      harryClimbing = false;
      harryClimbSpriteIndex = 0;
      fallingIntoUnderground = false;
      //recording = true;      

      if (realStart) {
        score = 0;
        treasures = 32;
        lives = 4;
        gameMode = GameMode.Playing;
      } else {
        demoDataIndex = 0;
        gameMode = GameMode.TitleScreen;
      }

      GenerateEnemies();
      for (int y = 0; y < 50; y++) {
        for (int x = 0; x < 80; x++) {
          hiddenBuffer[y, x].AsciiChar = (byte)0xdd;
        }
      }
            
      nextFrameStart = stopwatch.ElapsedTicks;
    }

    private void UpdateModel() {
      if (gameMode == GameMode.Ending) {
        UpdateEnding();
        return;
      }

      ProcessKeyboardEvents();

      // Move Enemies
      TunnelScene wallScene = null;
      if (underground) {
        harryHittingLog = false;
        harryKneeling = false;
        harryWalkingOnCrocodile = false;
        int sceneIndex = (int)(harryX / 320);
        wallScene = tunnelScenes[undergroundIndex, sceneIndex];
        harryLocation = wallScene.name;
        sceneIndex -= 2;
        for (int i = 0; i < 5; i++) {
          TunnelScene tunnelScene = tunnelScenes[undergroundIndex, sceneIndex];
          for (int j = tunnelScene.enemies.Count - 1; j >= 0; j--) {
            tunnelScene.enemies[j].Update(this);
          }
          sceneIndex++;
        }
      } else {
        harryHittingLog = false;
        harryKneeling = false;
        harryWalkingOnCrocodile = false;
        int sceneIndex = (int)(harryX / 320);
        harryLocation = scenes[sceneIndex].name;
        sceneIndex--;
        for (int i = 0; i < 3; i++) {
          Scene scene = scenes[sceneIndex];
          for (int j = scene.enemies.Count - 1; j >= 0; j--) {
            scene.enemies[j].Update(this);
          }
          sceneIndex++;
        }
      }

      // Move Harry
      if (gameMode == GameMode.Playing || gameMode == GameMode.TitleScreen) {
        if (harryDying) {
          switch (harryDyingState) {
            case HarryDyingState.Blinking:
              if (harryDyingDelay > 0) {
                harryDyingDelay -= 0.5;
              } else {
                LoseLife();
              }
              break;
            case HarryDyingState.Panning:
              if (((int)harryX) % 320 > 0) {
                harryX--;
              } else {
                harryDyingState = HarryDyingState.Dropping;
                harryY = -harryStandingSprite.Height;
                harryVy = 0;
              }
              break;
            case HarryDyingState.Dropping:
              harryVy += 0.02;
              harryY += harryVy;
              if (harryVy > 0 && harryY >= 24) {
                harryY = 24;
                harryDying = false;
              }
              break;
          }
        } else {
          harryRunning = false;
          lastHarryX = harryX;
          lastHarryY = harryY;
          if (!harryKneeling && !harrySwinging) {
            if (harryClimbing) {
              if (upKeyPressed) {
                harryY -= 0.2;
                harryClimbSpriteIndex += 0.07;
                if (harryClimbSpriteIndex >= 2) {
                  harryClimbSpriteIndex -= 2;
                }
                if (harryY < -16) {
                  underground = false;
                  harryClimbing = false;
                  fallingIntoUnderground = false;
                  int sceneIndex = (int)(harryX / 320);
                  while (sceneIndex < 0) {
                    sceneIndex += 85;
                  }
                  while (sceneIndex >= 85) {
                    sceneIndex -= 85;
                  }
                  harryX = (harryX % 320) + 2 * 320 * 255 + undergroundIndex * 320 + 3 * 320 * sceneIndex;
                  harryY = 24;
                  harryJumpState = HarryJumpState.Jumping;
                  harryVy = -0.8;
                }
              } else if (downKeyPressed) {
                if (harryY < 24) {
                  harryY += 0.2;
                } else if (harryY >= 24) {
                  harryY = 24;
                  harryClimbing = false;
                }
                harryClimbSpriteIndex += 0.07;
                if (harryClimbSpriteIndex >= 2) {
                  harryClimbSpriteIndex -= 2;
                }
              } else if (leftKeyPressed) {
                Ladder ladder = wallScene.ladder;
                if (harryX + 9 > ladder.x) {
                  harryX -= 0.2;
                  harryFacingRight = false;
                  harryRunning = true;
                  harryClimbSpriteIndex += 0.07;
                  if (harryClimbSpriteIndex >= 2) {
                    harryClimbSpriteIndex -= 2;
                  }
                }
              } else if (rightKeyPressed) {
                Ladder ladder = wallScene.ladder;
                if (harryX < ladder.x + 4) {
                  harryX += 0.2;
                  harryFacingRight = true;
                  harryRunning = true;
                  harryClimbSpriteIndex += 0.07;
                  if (harryClimbSpriteIndex >= 2) {
                    harryClimbSpriteIndex -= 2;
                  }
                }
              }
            } else if (leftKeyPressed) {
              harryX -= 1.0;
              harryFacingRight = false;
              harryRunning = true;
            } else if (rightKeyPressed) {
              harryX += 1.0;
              harryFacingRight = true;
              harryRunning = true;
            }
          }
          switch (harryJumpState) {
            case HarryJumpState.NotJumping:
              if (jumpKeyPressed && !harryKneeling && !harryDying) {
                harryClimbing = false;
                harrySwinging = false;
                harryJumpState = HarryJumpState.Jumping;
                harryVy = -0.8;
              } else if (underground && upKeyPressed && !harryClimbing) {
                Ladder ladder = wallScene.ladder;
                if (ladder != null && harryX + 9 >= ladder.x && harryX <= ladder.x + 4) {
                  harryClimbing = true;
                }
              }
              break;
            case HarryJumpState.Jumping:
              harryVy += 0.02;
              harryY += harryVy;
              if (harryVy > 0 && harryY >= 24) {
                harryY = 24;
                fallingIntoUnderground = false;
                if (jumpKeyPressed) {
                  harryJumpState = HarryJumpState.Landing;
                } else {
                  harryJumpState = HarryJumpState.NotJumping;
                }
              }
              break;
            case HarryJumpState.Landing:
              if (!jumpKeyPressed) {
                harryJumpState = HarryJumpState.NotJumping;
              }
              break;
          }
        }

        if (harryRunning) {
          if (!harryDying) {
            harryRunningIndex += 0.1;
            while (harryRunningIndex >= 5) {
              harryRunningIndex -= 5;
            }
          }
        } else {
          harryRunningIndex = 0;
        }

        // Move Camera
        cameraX = harryX - 72;

        if (gameMode == GameMode.Playing) {
          // Compute remaining time
          timeSB.Length = 0;
          remainingTicks--;
          if (remainingTicks <= 0) {
            timeSB.Append("Time: 00:00");
            gameMode = GameMode.GameOver;
            gameOverDelay = 5 * 120;
          } else {
            timeSB.Append("Time: ");
            int remainingSeconds = (remainingTicks / 120) % 60;
            int remainingMinutes = remainingTicks / 7200;
            if (remainingMinutes < 10) {
              timeSB.Append("0");
            }
            timeSB.Append(remainingMinutes).Append(":");
            if (remainingSeconds < 10) {
              timeSB.Append("0");
            }
            timeSB.Append(remainingSeconds);
          }
        } else {
          pressEnterIndex += 0.02;
          if (pressEnterIndex >= 2) {
            pressEnterIndex = 0;
          }
          if (enterKeyPressed) {
            StartGame(true);
          }
        }
      } else if (gameMode == GameMode.Ending) {
        UpdateEnding();
      } else {
        if (gameOverDelay > 0) {
          gameOverDelay--;
        } else {
          for (int x = 0; x < 80; x++) {
            hiddenBuffer[24, x].AsciiChar = (byte)0xdd;
          }
          StartGame(false);
        }
      }
    }

    private void RenderUnderground() {
      DrawTunnelBackground();

      int sceneIndex = (int)(harryX / 320);
      sceneIndex -= 2;
      for (int i = 0; i < 5; i++) {
        TunnelScene tunnelScene = tunnelScenes[undergroundIndex, sceneIndex];
        foreach (IEnemy enemy in tunnelScene.enemies) {
          enemy.Render(this);
        }
        sceneIndex++;
      }

      bool drawHarry = gameMode == GameMode.Playing || gameMode == GameMode.TitleScreen;

      if (drawHarry) {
        if (harryDying && harryDyingState == HarryDyingState.Dropping) {
          harryRunningSprites[4].Draw(
              (int)(harryX - cameraX), (int)harryY, hiddenBuffer, harryFacingRight);
        } else if (harryClimbing) {
          if (!harryDying || (harryDying && harryDyingState == HarryDyingState.Blinking
              && (((int)harryDyingDelay) & 1) == 0)) {
            harryClimbingSprite.Draw((int)(harryX - cameraX), (int)harryY, hiddenBuffer,
                harryClimbSpriteIndex > 1);
          }
        }

        if (fallingIntoUnderground) {
          harryRunningSprites[4].Draw(
              (int)(harryX - cameraX), (int)harryY, hiddenBuffer, harryFacingRight);
        }
      }

      DrawTunnelForeground();

      if (drawHarry && !harryKneeling && !harrySwinging && !harryClimbing) {
        if (!harryDying || (harryDying && harryDyingState == HarryDyingState.Blinking
            && (((int)harryDyingDelay) & 1) == 0)) {
          if (harryJumpState == HarryJumpState.Jumping) {
            if (!fallingIntoUnderground) {
              harryRunningSprites[4].Draw(
                  (int)(harryX - cameraX), (int)harryY, hiddenBuffer, harryFacingRight);
            }
          } else if (harryRunning) {
            harryRunningSprites[(int)harryRunningIndex].Draw(
                (int)(harryX - cameraX), (int)harryY, hiddenBuffer, harryFacingRight);
          } else {
            harryStandingSprite.Draw((int)(harryX - cameraX), (int)harryY, hiddenBuffer, harryFacingRight);
          }
        }
      }

      Print(harryLocation, 0, 0, ConsoleColor.White, ConsoleColor.Black);
      Print(string.Format("Score: {0}", score), 17, 0, ConsoleColor.White, ConsoleColor.Black);
      Print(string.Format("Treasures: {0}", treasures), 37, 0, ConsoleColor.White, ConsoleColor.Black);
      Print(string.Format("Lives: {0}", lives), 56, 0, ConsoleColor.White, ConsoleColor.Black);
      Print(timeSB.ToString(), 69, 0, ConsoleColor.White, ConsoleColor.Black);

      if (gameMode == GameMode.TitleScreen) {
        titleSprite.Draw((160 - titleSprite.Width) / 2, 3, hiddenBuffer);
        PrintCentered("D = Jump", 24, ConsoleColor.White, ConsoleColor.Black);
        PrintCentered("Arrow keys = Move", 26, ConsoleColor.White, ConsoleColor.Black);
        PrintCentered("Alt+Enter = Toggle full-screen mode", 28, ConsoleColor.White, ConsoleColor.Black);
        PrintCentered("Esc = Quit", 30, ConsoleColor.White, ConsoleColor.Black);
        if (pressEnterIndex >= 1) {
          PrintCentered("PRESS ENTER", 37, ConsoleColor.White, ConsoleColor.Black);
        } else {
          for (int x = 30; x < 50; x++) {
            hiddenBuffer[37, x].AsciiChar = (byte)0xdd;
          }
        }
        PrintCentered("(C) 2008 meatfighter.com", 49, ConsoleColor.White, ConsoleColor.Black);
      } else if (gameMode == GameMode.GameOver) {
        PrintCentered("G A M E   O V E R", 24, ConsoleColor.White, ConsoleColor.Black);
      }

      screenBuffer.WriteBlock(hiddenBuffer, 0, 0, 0, 0, 79, 49);
    }

    private void RenderJungle() {
      DrawForestBackground();

      int sceneIndex = (int)(harryX / 320);
      sceneIndex--;
      for (int i = 0; i < 3; i++) {
        Scene scene = scenes[sceneIndex];
        foreach (IEnemy enemy in scene.enemies) {
          enemy.Render(this);
        }
        sceneIndex++;
      }

      bool drawHarry = gameMode == GameMode.Playing || gameMode == GameMode.TitleScreen;

      if (drawHarry && harryDying && harryDyingState == HarryDyingState.Dropping) {
        harryRunningSprites[4].Draw(
            (int)(harryX - cameraX), (int)harryY, hiddenBuffer, harryFacingRight);
      }

      DrawForestForeground();

      if (drawHarry && !harryKneeling && !harrySwinging) {
        if (!harryDying || (harryDying && harryDyingState == HarryDyingState.Blinking
            && (((int)harryDyingDelay) & 1) == 0)) {
          if (harryJumpState == HarryJumpState.Jumping) {
            harryRunningSprites[4].Draw(
                (int)(harryX - cameraX), (int)harryY, hiddenBuffer, harryFacingRight);
          } else if (harryRunning) {
            harryRunningSprites[(int)harryRunningIndex].Draw(
                (int)(harryX - cameraX), (int)harryY, hiddenBuffer, harryFacingRight);
          } else {
            harryStandingSprite.Draw((int)(harryX - cameraX), (int)harryY, hiddenBuffer, harryFacingRight);
          }
        }
      }

      Print(harryLocation, 0, 0, ConsoleColor.White, ConsoleColor.Black);
      Print(string.Format("Score: {0}", score), 17, 0, ConsoleColor.White, ConsoleColor.Black);
      Print(string.Format("Treasures: {0}", treasures), 37, 0, ConsoleColor.White, ConsoleColor.Black);
      Print(string.Format("Lives: {0}", lives), 56, 0, ConsoleColor.White, ConsoleColor.Black);
      Print(timeSB.ToString(), 69, 0, ConsoleColor.White, ConsoleColor.Black);

      if (gameMode == GameMode.TitleScreen) {
        titleSprite.Draw((160 - titleSprite.Width) / 2, 3, hiddenBuffer);
        PrintCentered("D = Jump", 24, ConsoleColor.White, ConsoleColor.Black);
        PrintCentered("Arrow keys = Move", 26, ConsoleColor.White, ConsoleColor.Black);
        PrintCentered("Alt+Enter = Toggle full-screen mode", 28, ConsoleColor.White, ConsoleColor.Black);
        PrintCentered("Esc = Quit", 30, ConsoleColor.White, ConsoleColor.Black);
        if (pressEnterIndex >= 1) {
          PrintCentered("PRESS ENTER", 37, ConsoleColor.White, ConsoleColor.Black);
        } else {
          for (int x = 30; x < 50; x++) {
            hiddenBuffer[37, x].AsciiChar = (byte)0xdd;
          }
        }
        PrintCentered("(C) 2008 meatfighter.com", 49, ConsoleColor.White, ConsoleColor.Black);
      } else if (gameMode == GameMode.GameOver) {
        PrintCentered("G A M E   O V E R", 24, ConsoleColor.White, ConsoleColor.Black);
      }

      screenBuffer.WriteBlock(hiddenBuffer, 0, 0, 0, 0, 79, 49);
    }

    private void RenderFrame() {
      if (gameMode == GameMode.Ending) {
        RenderEnding();
      } else if (underground) {
        RenderUnderground();
      } else {
        RenderJungle();
      }
    }

    private void DrawForestForeground() {
      // Draw canopy 2
      int leafDepthOffset = ((int)cameraX) % leafDepths.Length;
      for (int x = 0; x < 160; x++) {
        int X = x >> 1;
        int offset = leafDepthOffset + x;
        if (offset >= leafDepths.Length) {
          offset -= leafDepths.Length;
        } else if (offset < 0) {
          offset += leafDepths.Length;
        }
        if ((x & 1) == 0) {
          for (int y = leafDepths[offset]; y > 0; y--) {
            hiddenBuffer[y, X].Foreground = ConsoleColor.DarkGreen;
          }
        } else {
          for (int y = leafDepths[offset]; y > 0; y--) {
            hiddenBuffer[y, X].Background = ConsoleColor.DarkGreen;
          }
        }
      }
    }

    private void DrawTunnelForeground() {
      // Draw canopy 2
      int caveDepthOffset = ((int)cameraX) % caveDepths.Length;
      for (int x = 0; x < 160; x++) {
        int X = x >> 1;
        int offset = caveDepthOffset + x;
        if (offset >= caveDepths.Length) {
          offset -= caveDepths.Length;
        } else if (offset < 0) {
          offset += caveDepths.Length;
        }
        if ((x & 1) == 0) {
          for (int y = caveDepths[offset]; y > 0; y--) {
            hiddenBuffer[y, X].Foreground = ConsoleColor.Gray;
          }
        } else {
          for (int y = caveDepths[offset]; y > 0; y--) {
            hiddenBuffer[y, X].Background = ConsoleColor.Gray;
          }
        }
      }
    }

    private void DrawTunnelBackground() {

      // Clear screen
      if (harryHittingLog || (harryDying && harryDyingState == HarryDyingState.Blinking)) {
        for (int x = 0; x < 80; x++) {
          hiddenBuffer[0, x].Foreground = ConsoleColor.Black;
          hiddenBuffer[0, x].Background = ConsoleColor.Black;
        }
        for (int y = 1; y < 50; y++) {
          for (int x = 0; x < 80; x++) {
            hiddenBuffer[y, x].Foreground = ConsoleColor.Red;
            hiddenBuffer[y, x].Background = ConsoleColor.Red;
          }
        }
      } else {
        for (int y = 0; y < 50; y++) {
          for (int x = 0; x < 80; x++) {
            hiddenBuffer[y, x].Foreground = ConsoleColor.Black;
            hiddenBuffer[y, x].Background = ConsoleColor.Black;
          }
        }
      }
  
      // Draw ground
      for (int y = 39; y < 50; y++) {
        for (int x = 0; x < 80; x++) {
          hiddenBuffer[y, x].Foreground = ConsoleColor.DarkGray;
          hiddenBuffer[y, x].Background = ConsoleColor.DarkGray;
        }
      }

      // Draw canopy 1
      int caveDepthOffset = ((int)(cameraX / 1.5)) % caveDepths.Length;
      for (int x = 0; x < 160; x++) {
        int X = x >> 1;
        int offset = caveDepthOffset + x;
        if (offset >= caveDepths.Length) {
          offset -= caveDepths.Length;
        } else if (offset < 0) {
          offset += caveDepths.Length;
        }
        if ((x & 1) == 0) {
          for (int y = caveDepths2[offset]; y > 0; y--) {
            hiddenBuffer[y, X].Foreground = ConsoleColor.DarkGray;
          }
        } else {
          for (int y = caveDepths2[offset]; y > 0; y--) {
            hiddenBuffer[y, X].Background = ConsoleColor.DarkGray;
          }
        }
      }
    }

    private void DrawForestBackground() {

      // Clear screen
      if (harryHittingLog || (harryDying && harryDyingState == HarryDyingState.Blinking)) {
        for (int x = 0; x < 80; x++) {
          hiddenBuffer[0, x].Foreground = ConsoleColor.Black;
          hiddenBuffer[0, x].Background = ConsoleColor.Black;
        }
        for (int y = 1; y < 50; y++) {
          for (int x = 0; x < 80; x++) {
            hiddenBuffer[y, x].Foreground = ConsoleColor.Red;
            hiddenBuffer[y, x].Background = ConsoleColor.Red;
          }
        }
      } else {
        for (int y = 0; y < 50; y++) {
          for (int x = 0; x < 80; x++) {
            hiddenBuffer[y, x].Foreground = ConsoleColor.Black;
            hiddenBuffer[y, x].Background = ConsoleColor.Black;
          }
        }
      }

      // Draw ground
      for (int y = 39; y < 50; y++) {
        for (int x = 0; x < 80; x++) {
          hiddenBuffer[y, x].Foreground = ConsoleColor.DarkGray;
          hiddenBuffer[y, x].Background = ConsoleColor.DarkGray;
        }
      }

      // Draw canopy 1
      int leafDepthOffset = ((int)(cameraX / 1.5)) % leafDepths.Length;
      for (int x = 0; x < 160; x++) {
        int X = x >> 1;
        int offset = leafDepthOffset + x;
        if (offset >= leafDepths.Length) {
          offset -= leafDepths.Length;
        } else if (offset < 0) {
          offset += leafDepths.Length;
        }
        if ((x & 1) == 0) {
          for (int y = leafDepths2[offset]; y > 0; y--) {
            hiddenBuffer[y, X].Foreground = ConsoleColor.DarkYellow;
          }
        } else {
          for (int y = leafDepths2[offset]; y > 0; y--) {
            hiddenBuffer[y, X].Background = ConsoleColor.DarkYellow;
          }
        }
      }
    }

    //byte[] recordedKeys = new byte[120 * 60 * 5];
    //int recordedKeysIndex = 0;
    //bool recording = false;

    private void ProcessKeyboardEvents() {
      if (inputBuffer.PeekEvents(inputEvents) > 0) {
        int eventsRead = inputBuffer.ReadEvents(inputEvents);
        for (int i = 0; i < eventsRead; i++) {
          if (inputEvents[i].EventType == ConsoleInputEventType.KeyEvent) {
            switch (inputEvents[i].KeyEvent.VirtualKeyCode) {
              case ConsoleKey.LeftArrow:
                leftKeyPressed = inputEvents[i].KeyEvent.KeyDown;
                break;
              case ConsoleKey.RightArrow:
                rightKeyPressed = inputEvents[i].KeyEvent.KeyDown;
                break;
              case ConsoleKey.UpArrow:
                upKeyPressed = inputEvents[i].KeyEvent.KeyDown;
                break;
              case ConsoleKey.DownArrow:
                downKeyPressed = inputEvents[i].KeyEvent.KeyDown;
                break;
              case ConsoleKey.Escape:
                Environment.Exit(0);
                break;
              case ConsoleKey.D:
                jumpKeyPressed = inputEvents[i].KeyEvent.KeyDown;
                break;
              case ConsoleKey.Enter:
                enterKeyPressed = inputEvents[i].KeyEvent.KeyDown;
                break;
            }
          }
        }
      }

      /*if (recording) {
        int value = 0;
        if (leftKeyPressed) {
          value |= 1;
        }
        if (rightKeyPressed) {
          value |= 2;
        }
        if (upKeyPressed) {
          value |= 4;
        }
        if (downKeyPressed) {
          value |= 8;
        }
        if (jumpKeyPressed) {
          value |= 16;
        }
        recordedKeys[recordedKeysIndex] = (byte)value;
        recordedKeysIndex++;
        if (recordedKeysIndex == recordedKeys.Length) {
          FileStream stream = new FileStream("demo.dat", FileMode.Create, FileAccess.Write);
          stream.Write(recordedKeys, 0, recordedKeys.Length);
          stream.Close();
          Environment.Exit(0);
        }
      }*/

      if (gameMode == GameMode.TitleScreen) {
        int value = demoData[demoDataIndex];
        leftKeyPressed = (value & 1) == 1;
        rightKeyPressed = (value & 2) == 2;
        upKeyPressed = (value & 4) == 4;
        downKeyPressed = (value & 8) == 8;
        jumpKeyPressed = (value & 16) == 16;
        demoDataIndex++;
        if (demoDataIndex == demoData.Length) {
          demoDataIndex = 0;
          StartGame(false);
        }
      }
    }

    private void GenerateEnemies() {
      for (int i = 0, sceneX = 0; i < scenes.Length; i++, sceneX += 320) {
        Scene scene = scenes[i];
        TunnelScene tunnelScene = tunnelScenes[i % 3, i / 3];
        tunnelScene.name = scene.name;
        tunnelScene.enemies.Clear();
        scene.enemies.Clear();
        int tunnelSceneX = 320 * (i / 3);

        if (scene.sceneType == SceneType.Hole_1 || scene.sceneType == SceneType.Hole_3) {
          int s = (i % 255) + 1;
          Sprite sprite = ladderHoleSprite;
          if (s == 12 || s == 38 || s == 117 || s == 186 || s == 225 || s == 236) {
            sprite = ladderHoleDropSprite;
          } else if (s == 26 || s == 93 || s == 227) {
            sprite = ladderHoleBacktrackSprite;
          }
          scene.enemies.Add(new LadderHole((int)(sceneX + (320 - ladderHoleSprite.Width) / 2), sprite));
          if (scene.wallType == WallType.Left) {
            tunnelScene.wall = new Wall((int)(tunnelSceneX + 32));
            tunnelScene.enemies.Add(tunnelScene.wall);
          } else {
            tunnelScene.wall = new Wall((int)(tunnelSceneX + 271));
            tunnelScene.enemies.Add(tunnelScene.wall);
          }
          tunnelScene.ladder = new Ladder(tunnelSceneX + (320 - ladderSprite.Width) / 2);
          tunnelScene.enemies.Add(tunnelScene.ladder);
        } else {
          tunnelScene.scorpion = new Scorpion(tunnelSceneX + 160 - scorpionSprites[0].Width / 2);
          tunnelScene.enemies.Add(tunnelScene.scorpion);
        }
        if (scene.sceneType == SceneType.Hole_3) {
          scene.enemies.Add(new TunnelHole((int)(sceneX + 45)));
          scene.enemies.Add(new TunnelHole((int)(sceneX + 275 - tunnelHoleSprite.Width)));
        }
        if (scene.sceneType == SceneType.TarPitWithVine) {
          scene.enemies.Add(new TarPit((int)(sceneX + (320 - tarPitSprite.Width) / 2) - 10));
        } else if (scene.sceneType == SceneType.QuickSandWithVine) {
          scene.enemies.Add(new QuickSand((int)(sceneX + (320 - quickSandSprite.Width) / 2) - 10));
        } else if (scene.sceneType == SceneType.CrocodilePit) {
          scene.enemies.Add(new QuickSand((int)(sceneX + (320 - quickSandSprite.Width) / 2) - 10));
          scene.enemies.Add(new Crocodile((int)(sceneX + 100)));
          scene.enemies.Add(new Crocodile((int)(sceneX + 174)));
        } else if (scene.sceneType == SceneType.CrocodilePitWithVine) {
          scene.enemies.Add(new QuickSand((int)(sceneX + (320 - quickSandSprite.Width) / 2) - 10));
          scene.enemies.Add(new Crocodile((int)(sceneX + 100)));
          scene.enemies.Add(new Crocodile((int)(sceneX + 174)));
        } else if (scene.sceneType == SceneType.ShiftingTarPitWithTreasure) {
          scene.enemies.Add(new ShiftingTarPit((int)(sceneX + (320 - quickSandSprite.Width) / 2) - 10));
          switch (scene.treasureType) {
            case TreasureType.Gold:
              scene.enemies.Add(new Gold(sceneX + 303));
              break;
            case TreasureType.Silver:
              scene.enemies.Add(new Silver(sceneX + 303));
              break;
            case TreasureType.Money:
              scene.enemies.Add(new Money(sceneX + 303));
              break;
            case TreasureType.Ring:
              scene.enemies.Add(new Ring(sceneX + 303));
              break;
          }
        } else if (scene.sceneType == SceneType.ShiftingTarPitWithVine) {
          scene.enemies.Add(new ShiftingTarPit((int)(sceneX + (320 - quickSandSprite.Width) / 2) - 10));
        } else if (scene.sceneType == SceneType.ShiftingQuickSand) {
          scene.enemies.Add(new ShiftingQuickSand((int)(sceneX + (320 - quickSandSprite.Width) / 2) - 10));
        }

        if (scene.sceneType != SceneType.CrocodilePit
            && scene.sceneType != SceneType.CrocodilePitWithVine
            && scene.sceneType != SceneType.ShiftingTarPitWithTreasure) {
          switch (scene.objectType) {
            case ObjectType.Log:
              scene.enemies.Add(new StationaryLog(sceneX + 303));
              break;
            case ObjectType.Log_3:
              scene.enemies.Add(new StationaryLog(sceneX + 112));
              scene.enemies.Add(new StationaryLog(sceneX + 192));
              scene.enemies.Add(new StationaryLog(sceneX + 288));
              break;
            case ObjectType.RollingLog:
              scene.enemies.Add(new RollingLog(sceneX, 0));
              break;
            case ObjectType.RollingLog_2a:
              scene.enemies.Add(new RollingLog(sceneX, 0));
              scene.enemies.Add(new RollingLog(sceneX, 120));
              break;
            case ObjectType.RollingLog_2b:
              scene.enemies.Add(new RollingLog(sceneX, 0));
              scene.enemies.Add(new RollingLog(sceneX, 240));
              break;
            case ObjectType.RollingLog_3:
              scene.enemies.Add(new RollingLog(sceneX, 0));
              scene.enemies.Add(new RollingLog(sceneX, 200));
              scene.enemies.Add(new RollingLog(sceneX, 400));
              break;
            case ObjectType.Fire:
              scene.enemies.Add(new Fire(sceneX + 303));
              break;
            case ObjectType.Cobra:
              scene.enemies.Add(new Cobra(sceneX + 303));
              break;
          }
        }

        if (scene.sceneType == SceneType.TarPitWithVine) {
           scene.enemies.Add(new Vine(sceneX + 155));
        } else if (scene.sceneType == SceneType.QuickSandWithVine) {
          scene.enemies.Add(new Vine(sceneX + 155));
        } else if (scene.sceneType == SceneType.CrocodilePitWithVine) {
          scene.enemies.Add(new Vine(sceneX + 155));
        } else if (scene.sceneType == SceneType.ShiftingTarPitWithVine) {
          scene.enemies.Add(new Vine(sceneX + 155));
        } 
      }
    }

    public void LoseLife() {
      if (gameMode == GameMode.Playing) {
        lives--;
        if (lives < 0) {
          lives = 0;
          gameMode = GameMode.GameOver;
          gameOverDelay = 5 * 120;
          return;
        }
      }
      harryDyingState = HarryDyingState.Panning;
      if (underground) {
        underground = false;
        harryClimbing = false;
        fallingIntoUnderground = false;
        int sceneIndex = (int)(harryX / 320);
        while(sceneIndex < 0) {
          sceneIndex += 85;
        }
        while(sceneIndex >= 85) {
          sceneIndex -= 85;
        }
        harryX = (harryX % 320) + 2 * 320 * 255 + undergroundIndex * 320 + 3 * 320 * sceneIndex;
      }
    }

    private void GenerateMap() {
      for (int i = 0; i < tunnelScenes.GetLength(0); i++) {
        for (int j = 0; j < tunnelScenes.GetLength(1); j++) {
          tunnelScenes[i, j] = new TunnelScene();
        }
      }

      int random = 0xc4;
      for (int i = 0; i < scenes.Length; i++) {

        Scene scene = new Scene();
        scenes[i] = scene;

        int bit0 = 1 & (random >> 0);
        int bit1 = 1 & (random >> 1);
        int bit2 = 1 & (random >> 2);
        int bit3 = 1 & (random >> 3);
        int bit4 = 1 & (random >> 4);
        int bit5 = 1 & (random >> 5);
        int bit6 = 1 & (random >> 6);
        int bit7 = 1 & (random >> 7);

        int sceneType = 7 & (random >> 3);
        switch (sceneType) {
          case 0:
            scene.sceneType = SceneType.Hole_1;
            break;
          case 1:
            scene.sceneType = SceneType.Hole_3;
            break;
          case 2:
            scene.sceneType = SceneType.TarPitWithVine;
            break;
          case 3:
            scene.sceneType = SceneType.QuickSandWithVine;
            break;
          case 4:
            if (bit1 == 0) {
              scene.sceneType = SceneType.CrocodilePit;
            } else {
              scene.sceneType = SceneType.CrocodilePitWithVine;
            }
            break;
          case 5:
            scene.sceneType = SceneType.ShiftingTarPitWithTreasure;
            break;
          case 6:
            scene.sceneType = SceneType.ShiftingTarPitWithVine;
            break;
          case 7:
            scene.sceneType = SceneType.ShiftingQuickSand;
            break;
        }

        scene.treasureType = (TreasureType)(random & 3);
        scene.objectType = (ObjectType)(random & 7);
        scene.wallType = (WallType)bit7;
        scene.backgroundType = 3 & (random >> 6);

        random = (random << 1) | (bit3 ^ bit4 ^ bit5 ^ bit7);

        scene.name = string.Format("Location: {0}", (i % 255) + 1);
      }
    }

    private void GenerateBackground() {

      double x = 0, y = 0.3, z = 0.5;
      int length = 8 * 160;
      double dx = 2.0 * Math.PI / (length / 4.0);
      double dy = 2.0 * Math.PI / (length / 16.0);
      double dz = 2.0 * Math.PI / (length / 32.0);
      for (int i = 0; i < length; i++) {
        leafDepths[i] = (int)(12 + 4 * Math.Sin(x) + 1 * Math.Sin(y) + 1.5 * Math.Sin(z));
        x += dx;
        y += dy;
        z += dz;
      }

      x = 1.6;
      y = 0.1;
      z = 0;
      length = 8 * 160;
      dx = 2.0 * Math.PI / (length / 8.0);
      dy = 2.0 * Math.PI / (length / 32.0);
      dz = 2.0 * Math.PI / (length / 64.0);
      for (int i = 0; i < length; i++) {
        leafDepths2[i] = (int)(13 + 3 * Math.Sin(x) + 2 * Math.Sin(y) + 1.75 * Math.Sin(z));
        x += dx;
        y += dy;
        z += dz;
      }

      x = 0.25;
      y = 0.3;
      z = 2.5;
      length = 8 * 160;
      dx = 2.0 * Math.PI / (length / 8.0);
      dy = 2.0 * Math.PI / (length / 32.0);
      dz = 2.0 * Math.PI / (length / 128.0);
      for (int i = 0; i < length; i++) {
        caveDepths[i] = (int)(4 + 8 * SawTooth(x) + 6 * SawTooth(y) + 4 * SawTooth(z));
        x += dx;
        y += dy;
        z += dz;
      }

      x = 0;
      y = 0.3;
      z = 0.5;
      length = 8 * 160;
      dx = 2.0 * Math.PI / (length / 4.0);
      dy = 2.0 * Math.PI / (length / 16.0);
      dz = 2.0 * Math.PI / (length / 32.0);
      for (int i = 0; i < length; i++) {
        caveDepths2[i] = (int)(16 + 4 * Math.Sin(x) + 1 * Math.Sin(y) + 1.5 * Math.Sin(z));
        x += dx;
        y += dy;
        z += dz;
      }
    }

    private double SawTooth(double angle) {
      if (angle < 0) {
        return -SawTooth(angle);
      }

      angle %= 2.0 * Math.PI;

      if (angle <= Math.PI) {
        return angle / Math.PI;
      } else {
        return (2.0 * Math.PI - angle) / Math.PI;
      }
    }

    private void PrintCentered(string s, int y, ConsoleColor foregroundColor, ConsoleColor backgroundColor) {
      PrintCentered(s, y, foregroundColor, backgroundColor, s.Length);
    }

    private void PrintCentered(string s, int y, ConsoleColor foregroundColor, ConsoleColor backgroundColor, 
        int length) {
      Print(s, (80 - s.Length) / 2, y, foregroundColor, backgroundColor, length);
    }

    private void Print(
        string s, int x, int y, ConsoleColor foregroundColor, ConsoleColor backgroundColor) {
      Print(s, x, y, foregroundColor, backgroundColor, s.Length);
    }

    private void Print(
        string s, int x, int y, ConsoleColor foregroundColor, ConsoleColor backgroundColor, int length) {
      if (y >= 0 && y < 50) {
        for (int i = length - 1; i >= 0; i--) {
          int X = x + i;
          hiddenBuffer[y, X].AsciiChar = (byte)s[i];
          hiddenBuffer[y, X].Foreground = foregroundColor;
          hiddenBuffer[y, X].Background = backgroundColor;
        }
      }
    }

    private void UpdateEnding() {
      switch (endingState) {
        case EndingState.Init:
          endingState = EndingState.Message;
          endingStringCharIndex = 0;
          endingStringIndex = 0;
          characterDelay = 0;
          break;
        case EndingState.Message:
          if (characterDelay > 0) {
            characterDelay--;
          } else if (endingStringIndex == 2) {
            creditsY = 55;
            endingState = EndingState.Credits;
          } else {
            endingStringCharIndex++;
            characterDelay = 12;
            if (endingStringCharIndex == endingStrings[endingStringIndex].Length) {
              endingStringIndex++;
              endingStringCharIndex = 0;
              if (endingStringIndex == 1) {
                characterDelay = 120;
              } else if (endingStringIndex == 2) {
                characterDelay = 120 * 5;
              }
            }            
          }
          break;
        case EndingState.Credits:
          creditsY -= 0.02;          
          harryRunningIndex += 0.1;
          while (harryRunningIndex >= 5) {
            harryRunningIndex -= 5;
          }
          if (creditsY < -44) {
            endingState = EndingState.Characters;
            characterState = CharacterState.ScrollIn;
            characterX = 162;
            characterX2 = (160 - characterSprites[characterIndex].Width) / 2;
            for (int y = 0; y < 50; y++) {
              for (int x = 0; x < 80; x++) {
                hiddenBuffer[y, x].AsciiChar = (byte)0xdd;
              }
            }
          }
          break;
        case EndingState.Characters:
          switch (characterState) {
            case CharacterState.ScrollIn:
              if (characterX > characterX2) {
                characterX -= 1;
              } else {
                characterDelay = 2 * 120;
                characterState = CharacterState.Pause;
                characterX2 = -(characterSprites[characterIndex].Width + 2);
              }
              break;
            case CharacterState.Pause:
              if (characterDelay > 0) {
                characterDelay--;
              } else {
                characterState = CharacterState.ScrollOut;
              }
              break;
            case CharacterState.ScrollOut:
              if (characterX > characterX2) {
                characterX -= 1;
              } else {
                for (int y = 0; y < 50; y++) {
                  for (int x = 0; x < 80; x++) {
                    hiddenBuffer[y, x].AsciiChar = (byte)0xdd;
                  }
                }        
                characterIndex++;
                if (characterIndex < characterSprites.Length) {
                  characterState = CharacterState.ScrollIn;
                  characterX = 162;
                  characterX2 = (160 - characterSprites[characterIndex].Width) / 2;
                } else {
                  endingState = EndingState.TheEnd;
                  characterDelay = 10 * 120;
                }
              }
              break;
          }
          break;
        case EndingState.TheEnd:
          if (characterDelay > 0) {
            characterDelay--;
          } else {
            for (int y = 0; y < 50; y++) {
              for (int x = 0; x < 80; x++) {
                hiddenBuffer[y, x].AsciiChar = (byte)0xdd;
              }
            }
            StartGame(false);
          }
          break;
      }
    }

    private void RenderEnding() {
      for (int y = 0; y < 50; y++) {
        for (int x = 0; x < 80; x++) {
          hiddenBuffer[y, x].Foreground = ConsoleColor.Black;
          hiddenBuffer[y, x].Background = ConsoleColor.Black;
        }
      }

      switch (endingState) {
        case EndingState.Init:
          break;
        case EndingState.Message:
          switch (endingStringIndex) {
            case 0:
              PrintCentered(endingStrings[0], 20, ConsoleColor.White, ConsoleColor.Black, endingStringCharIndex);
              break;
            case 1:
              PrintCentered(endingStrings[0], 20, ConsoleColor.White, ConsoleColor.Black);
              PrintCentered(endingStrings[1], 22, ConsoleColor.White, ConsoleColor.Black, endingStringCharIndex);
              break;
            case 2:
              PrintCentered(endingStrings[0], 20, ConsoleColor.White, ConsoleColor.Black);
              PrintCentered(endingStrings[1], 22, ConsoleColor.White, ConsoleColor.Black);
              break;
          } 
          break;
        case EndingState.Credits:
          Print("         CREDITS", 30, (int)creditsY, ConsoleColor.White, ConsoleColor.Black);
          Print("Game Programmer", 30, 6 + (int)creditsY, ConsoleColor.White, ConsoleColor.Black);
          Print("  Michael Birken", 30, 8 + (int)creditsY, ConsoleColor.Yellow, ConsoleColor.Black);
          Print("Console Library Programmer", 30, 12 + (int)creditsY, ConsoleColor.White, ConsoleColor.Black);
          Print("  Jim Mischel", 30, 14 + (int)creditsY, ConsoleColor.Yellow, ConsoleColor.Black);
          Print("Original Pitfall! Concept", 30, 18 + (int)creditsY, ConsoleColor.White, ConsoleColor.Black);
          Print("  David Crane / Activision", 30, 20 + (int)creditsY, ConsoleColor.Yellow, ConsoleColor.Black);
          Print("ASCII Pitfall!", 30, 40 + (int)creditsY, ConsoleColor.White, ConsoleColor.Black);
          Print("(C) 2008 meatfighter.com", 30, 42 + (int)creditsY, ConsoleColor.White, ConsoleColor.Black);

          harryRunningSprites[(int)harryRunningIndex].Draw(
              10, 24, hiddenBuffer, true);
          break;
        case EndingState.Characters:
          characterSprites[characterIndex].Draw((int)characterX,
              (50 - characterSprites[characterIndex].Height) / 2, hiddenBuffer);
          if (characterState == CharacterState.Pause) {
            PrintCentered(characterNames[characterIndex],
              (50 - characterSprites[characterIndex].Height) / 2 + characterSprites[characterIndex].Height
                + 1, ConsoleColor.White, ConsoleColor.Black);
          } 
          break;
        case EndingState.TheEnd:
          PrintCentered("Thanks for playing", 2, ConsoleColor.White, ConsoleColor.Black);
          PrintCentered(string.Format("Score: {0}", score), 6, ConsoleColor.White, ConsoleColor.Black);
          PrintCentered(timeSB.ToString(), 8, ConsoleColor.White, ConsoleColor.Black);
          PrintCentered(string.Format("Lives: {0}", lives), 10, ConsoleColor.White, ConsoleColor.Black);
          PrintCentered("T H E   E N D", 21, ConsoleColor.White, ConsoleColor.Black);
          harryStandingSprite.Draw((160 - harryStandingSprite.Width) / 2, 24, hiddenBuffer);
          break;
      }

      screenBuffer.WriteBlock(hiddenBuffer, 0, 0, 0, 0, 79, 49);
    }

    static void Main(string[] args) {
      new Program();
    }
  }
}
