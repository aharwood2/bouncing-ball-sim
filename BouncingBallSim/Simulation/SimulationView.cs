using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Xamarin.Forms;

namespace BouncingBallSim.Simulation
{
    class SimulationView : SKCanvasView
    {
        public float BallDiameter { get; set; }

        public bool IsActive { get; set; } = true;

        public float AccelerationDueToGravity { get; set; } = 981f;

        public float CoefficientOfRestitution { get; set; } = 0.5f;

        public float InitialVelocityX { get; set; } = 0f;

        public float InitialVelocityY { get; set; } = 0f;

        public float InitialPositionX { get; set; } = 0f;

        public float InitialPositionY { get; set; } = 0f;

        public SimulationView()
        {
            PaintSurface += SimulationView_PaintSurface;
            EnableTouchEvents = true;
            Init();
        }

        // Fields
        private SKPoint skPosition;
        private Vector2 skVelocity;
        private Vector2 skForce;
        private SKRect skBallBounds = default;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private const double desiredFps = 30.0;
        private float canvasWidth;
        private float canvasHeight;
        private bool isInitialised;
        private float skBallDiameter;
        private int fpsCount;
        private double fpsSum;

        private void Init()
        {
            var ms = 1000.0 / desiredFps;
            var ts = TimeSpan.FromMilliseconds(ms);

            // Create a timer that triggers every 1/fps seconds
            Device.StartTimer(ts, Update);
        }

        private bool Update()
        {
            // Get the elapsed time from the stopwatch because the 1/fps timer interval is not accurate and can be off by 2 ms
            var dt = stopwatch.Elapsed.TotalSeconds;

            // Restart the time measurement for the next time this method is called
            stopwatch.Restart();

            // As long as we have initialised we can simulate
            if (isInitialised)
            {
                // Reset force vector to gravity only
                skForce.X = 0;
                skForce.Y = AccelerationDueToGravity;

                // Collision detection -- compute change in momentum to work out the force required
                if ((skBallBounds.Left < 0 && skVelocity.X < 0) || (skBallBounds.Right > canvasWidth && skVelocity.X > 0))
                {
                    skForce.X = -(float)(skVelocity.X * (1 + CoefficientOfRestitution) / dt);
                    if (skBallBounds.Left < 0 && skVelocity.X < 0) skPosition.X += skBallBounds.Left;
                    else skPosition.X += canvasWidth - skBallBounds.Right;
                }

                if ((skBallBounds.Top < 0 && skVelocity.Y < 0) || (skBallBounds.Bottom > canvasHeight && skVelocity.Y > 0))
                {
                    skForce.Y = -(float)(skVelocity.Y * (1 + CoefficientOfRestitution) / dt);
                    if (skBallBounds.Top < 0 && skVelocity.Y < 0) skPosition.Y += skBallBounds.Top;
                    else skPosition.Y += canvasHeight - skBallBounds.Bottom;
                }

                // Compute du from force
                skVelocity.Y += (float)dt * skForce.Y;
                skVelocity.X += (float)dt * skForce.X;

                // Compute dr from velocity
                skPosition.X += (float)dt * skVelocity.X;
                skPosition.Y += (float)dt * skVelocity.Y;

                // Update the bounds
                skBallBounds = new SKRect(
                    skPosition.X - skBallDiameter / 2,
                    skPosition.Y - skBallDiameter / 2,
                    skPosition.X + skBallDiameter / 2,
                    skPosition.Y + skBallDiameter / 2
                    );
            }

            // Calculate current fps
            var fps = dt > 0 ? 1.0 / dt : 0;

            // Update the average FPS
            fpsCount++;
            fpsSum += fps;
            if (fpsCount == 20)
            {
                //Debug.WriteLine($"Average FPS = {fpsSum / fpsCount}");
                fpsCount = 0;
                fpsSum = 0;
            }

            // When the fps is too low reduce the load by skipping the frame
            if (fps < desiredFps / 2)
            {
                return IsActive;
            }

            // Trigger the redrawing of the view
            InvalidateSurface();

            return IsActive;
        }

        private void SimulationView_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            // Get info
            var info = e.Info;
            var surface = e.Surface;
            var canvas = surface.Canvas;
            canvas.Clear();
            
            // Initialise if first pass
            if (!isInitialised)
            {
                // Convert values to pixels
                skBallDiameter = BallDiameter * (float)Xamarin.Essentials.DeviceDisplay.MainDisplayInfo.Density;
                var skInitX = InitialPositionX * (float)Xamarin.Essentials.DeviceDisplay.MainDisplayInfo.Density;
                var skInitY = InitialPositionY * (float)Xamarin.Essentials.DeviceDisplay.MainDisplayInfo.Density;
                var skInitVX = InitialVelocityX * (float)Xamarin.Essentials.DeviceDisplay.MainDisplayInfo.Density;
                var skInitVY = InitialVelocityY * (float)Xamarin.Essentials.DeviceDisplay.MainDisplayInfo.Density;

                // Set limits
                canvasWidth = info.Width;
                canvasHeight = info.Height;

                // Initialise from properties
                skVelocity = new Vector2(skInitVX, skInitVY);
                if (skInitX - skBallDiameter / 2 < 0) skInitX = skBallDiameter / 2;
                else if (skInitX + skBallDiameter / 2 > canvasWidth) skInitX = canvasWidth - (skBallDiameter / 2);
                if (skInitY - skBallDiameter / 2 < 0) skInitY = skBallDiameter / 2;
                else if (skInitY + skBallDiameter / 2 > canvasHeight) skInitY = canvasHeight - (skBallDiameter / 2);
                skPosition = new SKPoint(skInitX, skInitY);

                // Create the bounds
                skBallBounds = new SKRect(
                    skPosition.X - skBallDiameter / 2,
                    skPosition.Y - skBallDiameter / 2,
                    skPosition.X + skBallDiameter / 2,
                    skPosition.Y + skBallDiameter / 2
                    );

                // Set flag
                isInitialised = true;
                Debug.WriteLine("Simulation Initialised!");
            }

            // Draw the ball
            using (SKPaint paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = Color.Orange.ToSKColor(),
                IsAntialias = true
            })
            {
                canvas.DrawCircle(skBallBounds.MidX, skBallBounds.MidY, skBallBounds.Width / 2f, paint);
            }
        }

        protected override void OnTouch(SKTouchEventArgs e)
        {
            base.OnTouch(e);
        }
    }
}
