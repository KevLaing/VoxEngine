using Silk.NET.Windowing;
using VoxEngine;

var options = WindowOptions.Default;
options.Size = new Silk.NET.Maths.Vector2D<int>(1920, 1080);
options.WindowState = WindowState.Fullscreen;
options.Title = "Voxel Engine: Altered State Prototype";

using var window = Window.Create(options);
using var game = new Game(window);

game.Run();
