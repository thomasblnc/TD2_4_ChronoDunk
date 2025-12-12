using Projet_sae;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ChronoDunk
{
    public partial class UCModeEntrainement : UserControl
    {
        // --- TIMERS ---
        DispatcherTimer gameTimer = new DispatcherTimer();

        // --- CONSTANTES ---
        const double WINDOW_WIDTH = 1280;
        const double WINDOW_HEIGHT = 720;
        const double GROUND_Y = 650;
        const double GRAVITY = 1.5;

        // --- ETAT ---
        bool isPaused = false;

        // --- JOUEUR ---
        double baseSpeed = 12;
        double jumpForce = -28;
        double sprintBonus = 8;
        double playerVelY = 0;
        bool playerJumping = false;
        bool playerHasBall = false;
        int superCharge = 0;

        // --- BALLON ---
        double ballX, ballY, ballVelX, ballVelY;
        double ballAngle = 0;
        int pickupCooldown = 0;
        bool isSuperShot = false;
        bool isAiming = false;

        // --- EFFETS ---
       
        List<Particle> particles = new List<Particle>();
        Random rnd = new Random();
        class Particle { public Rectangle Shape; public double VelX, VelY; public int Life; }

        public UCModeEntrainement(CharacterStats p1Stats = null)
        {
            InitializeComponent();

            // Charger le skin du joueur
            if (p1Stats != null)
            {
                baseSpeed = p1Stats.Speed;
                jumpForce = p1Stats.JumpForce;
                try { Player.Source = new BitmapImage(new Uri(p1Stats.ImagePath, UriKind.Relative)); } catch { }
            }

            this.Loaded += (s, e) =>
            {
                this.Focus();
                // Redimensionnement fenêtre pour être sûr
                Window win = Application.Current.MainWindow;
                win.Width = WINDOW_WIDTH;
                win.Height = WINDOW_HEIGHT;
                win.Left = (SystemParameters.PrimaryScreenWidth - win.Width) / 2;
                win.Top = (SystemParameters.PrimaryScreenHeight - win.Height) / 2;

                ResetBall(); // Place la balle au début
            };

            gameTimer.Interval = TimeSpan.FromMilliseconds(16);
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        private void ResetBall()
        {
            // Balle au centre
            ballX = (WINDOW_WIDTH / 2) - 40;
            ballY = 200;
            ballVelX = 0; ballVelY = 0; ballAngle = 0;
            isSuperShot = false;
            playerHasBall = false;
            pickupCooldown = 0;
            isAiming = false;
            TrajectoryLine.Visibility = Visibility.Collapsed;
            UpdateBallVisuals();
        }

        private void TogglePause()
        {
            if (isPaused)
            {
                isPaused = false;
                PauseMenu.Visibility = Visibility.Collapsed;
                gameTimer.Start();
                this.Focus();
            }
            else
            {
                // En pause, on cache la visée pour faire propre
                isAiming = false;
                TrajectoryLine.Visibility = Visibility.Collapsed;

                isPaused = true;
                PauseMenu.Visibility = Visibility.Visible;
                gameTimer.Stop();
            }
        }

        private void GameLoop(object sender, EventArgs e)
        {
            if (isPaused) return;

            if (pickupCooldown > 0) pickupCooldown--;

            // Déplacements
            double currentSpeed = Keyboard.IsKeyDown(Key.LeftShift) ? (baseSpeed + sprintBonus) : baseSpeed;
            if (Keyboard.IsKeyDown(Key.Left)) { MoveChar(-currentSpeed); ((ScaleTransform)Player.RenderTransform).ScaleX = -1; }
            if (Keyboard.IsKeyDown(Key.Right)) { MoveChar(currentSpeed); ((ScaleTransform)Player.RenderTransform).ScaleX = 1; }
            if (Keyboard.IsKeyDown(Key.Up) && !playerJumping) { playerVelY = jumpForce; playerJumping = true; }

            // Physique Joueur
            ApplyPhysics();

            // Physique Balle
            HandleBallLogic();

            // Collisions Panier
            CheckCollisions();

            // Particules (Juste pour le style si on marque)
            UpdateParticles();

            SuperBar.Value = superCharge;
        }

        private void MoveChar(double amount)
        {
            double newLeft = Canvas.GetLeft(Player) + amount;
            if (newLeft > -50 && newLeft < WINDOW_WIDTH - 150) Canvas.SetLeft(Player, newLeft);
        }

        private void ApplyPhysics()
        {
            double top = Canvas.GetTop(Player) + playerVelY;
            playerVelY += GRAVITY;
            if (top + Player.Height >= GROUND_Y)
            {
                top = GROUND_Y - Player.Height;
                playerJumping = false;
                playerVelY = 0;
            }
            Canvas.SetTop(Player, top);
        }

        private void HandleBallLogic()
        {
            if (playerHasBall)
            {
                ballX = Canvas.GetLeft(Player) + (Player.Width / 2) - 40;
                ballY = Canvas.GetTop(Player) + (Player.Height / 3);
                ballVelX = 0; ballVelY = 0; isSuperShot = false;
            }
            else
            {
                ballX += ballVelX; ballY += ballVelY; ballVelY += GRAVITY * 0.8; ballAngle += ballVelX * 2;

                RotateTransform rotate = Ball.RenderTransform as RotateTransform;
                if (rotate == null) { rotate = new RotateTransform(); Ball.RenderTransform = rotate; }
                rotate.Angle = ballAngle;

                // Rebond sol
                if (ballY + 80 >= GROUND_Y)
                {
                    ballY = GROUND_Y - 80;
                    ballVelY = -ballVelY * 0.6;
                    ballVelX *= 0.95;
                }

                // Si la balle sort de l'écran, on la remet au centre
                if (ballX > WINDOW_WIDTH + 100 || ballX < -100) ResetBall();

                // Ramassage
                if (pickupCooldown == 0)
                {
                    Rect ballRect = new Rect(ballX, ballY, 80, 80);
                    Rect playerRect = new Rect(Canvas.GetLeft(Player) + 50, Canvas.GetTop(Player), 100, 300);
                    if (playerRect.IntersectsWith(ballRect)) { playerHasBall = true; isAiming = false; }
                }
            }
            UpdateBallVisuals();
        }

        private void UpdateBallVisuals()
        {
            Ball.Opacity = (isSuperShot && DateTime.Now.Millisecond % 100 < 50) ? 0.5 : 1.0;
            Canvas.SetLeft(Ball, ballX); Canvas.SetTop(Ball, ballY);
        }

        private void CheckCollisions()
        {
            if (playerHasBall) return;
            Rect b = new Rect(ballX, ballY, 80, 80);

            // Panier Droite
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(HitboxBackboardRight), Canvas.GetTop(HitboxBackboardRight), 10, 100))) ballVelX = -ballVelX * 0.6;

            // Si on marque
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(HitboxHoopRight), Canvas.GetTop(HitboxHoopRight), 50, 10)) && ballVelY > 0)
            {
                // Juste des effets visuels, pas de score
                SpawnConfetti(Canvas.GetLeft(HitboxHoopRight), Canvas.GetTop(HitboxHoopRight));
                superCharge = 100; // Récompense : Super tir chargé
                ResetBall(); // On redonne la balle
            }
        }

        // --- GESTION SOURIS (TIR) ---
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isPaused) return;
            if (playerHasBall) { isAiming = true; TrajectoryLine.Visibility = Visibility.Visible; TrajectoryLine.Stroke = superCharge >= 100 ? Brushes.Red : Brushes.Yellow; }
        }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (isPaused || !isAiming || !playerHasBall) return;
            Point mousePos = e.GetPosition(GameCanvas);
            double velX = (mousePos.X - ballX) * 0.15; double velY = (mousePos.Y - ballY) * 0.15;
            if (velX > 35) velX = 35; if (velX < -35) velX = -35; if (velY > 35) velY = 35; if (velY < -35) velY = -35;

            // Dessin trajectoire
            PointCollection points = new PointCollection();
            for (int i = 0; i < 15; i++) { double t = i * 2; points.Add(new Point(ballX + velX * t, ballY + velY * t + 0.5 * (GRAVITY * 0.8) * t * t)); }
            TrajectoryLine.Points = points;
        }
        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isPaused || !isAiming || !playerHasBall) return;
            isAiming = false; TrajectoryLine.Visibility = Visibility.Collapsed;
            Point mousePos = e.GetPosition(GameCanvas);
            ballVelX = (mousePos.X - ballX) * 0.15; ballVelY = (mousePos.Y - ballY) * 0.15;
            if (superCharge >= 100) { isSuperShot = true; ballVelX = 25; ballVelY = -15; superCharge = 0; }
            playerHasBall = false; pickupCooldown = 30;
        }

        // --- EFFETS ---
        private void SpawnConfetti(double x, double y)
        {
            for (int i = 0; i < 20; i++)
            {
                Rectangle rect = new Rectangle { Width = 8, Height = 8 };
                byte r = (byte)rnd.Next(256); byte g = (byte)rnd.Next(256); byte b = (byte)rnd.Next(256);
                rect.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
                Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y); GameCanvas.Children.Add(rect);
                particles.Add(new Particle { Shape = rect, VelX = rnd.NextDouble() * 10 - 5, VelY = rnd.NextDouble() * 10 - 5, Life = 60 });
            }
        }
        private void UpdateParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i]; p.Life--;
                Canvas.SetLeft(p.Shape, Canvas.GetLeft(p.Shape) + p.VelX); Canvas.SetTop(p.Shape, Canvas.GetTop(p.Shape) + p.VelY); p.VelY += 0.5;
                if (p.Life <= 0) { GameCanvas.Children.Remove(p.Shape); particles.RemoveAt(i); }
            }
        }

        // --- BOUTONS ---
        private void PauseButton_Click(object sender, RoutedEventArgs e) => TogglePause();
        private void Resume_Click(object sender, RoutedEventArgs e) => TogglePause();
        private void QuitToMenu_Click(object sender, RoutedEventArgs e)
        {
            gameTimer.Stop();
            Window win = Application.Current.MainWindow;
            win.Content = new UCMenuPrincipal();
            win.Width = 820; win.Height = 640;
            win.Left = (SystemParameters.PrimaryScreenWidth - win.Width) / 2;
            win.Top = (SystemParameters.PrimaryScreenHeight - win.Height) / 2;
        }
        private void GameCanvas_Click(object sender, MouseButtonEventArgs e) => this.Focus();
        private void OnKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) TogglePause(); }
        private void OnKeyUp(object sender, KeyEventArgs e) { }
    }
}