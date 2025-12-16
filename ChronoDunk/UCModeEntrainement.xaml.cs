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
        bool useZQSD = false;

        // --- JOUEUR ---
        double baseSpeed = 12;
        double jumpForce = -28;
        double sprintBonus = 8;
        double playerVelY = 0;
        bool playerJumping = false;
        bool playerHasBall = false;
        int superCharge = 0;
        int aiScore = 0;
        int playerScore = 0;

        // --- BALLON ---
        double ballX, ballY, ballVelX, ballVelY;
        double ballAngle = 0;
        int pickupCooldown = 0;
        bool isSuperShot = false;
        bool isAiming = false;

        // --- EFFETS ---
        List<Particle> particles = new List<Particle>();
        Random rnd = new Random();

        class Particle
        {
            public Rectangle Shape;
            public double VelX, VelY;
            public int Life;
        }

        public UCModeEntrainement(CharacterStats p1Stats = null)
        {
            InitializeComponent();

            // Charger le skin du joueur
            if (p1Stats != null)
            {
                baseSpeed = p1Stats.Speed;
                jumpForce = p1Stats.JumpForce;
                try
                {
                    Joueur.Source = new BitmapImage(new Uri(p1Stats.ImagePath, UriKind.Relative));
                }
                catch { }
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
            ballVelX = 0;
            ballVelY = 0;
            ballAngle = 0;
            isSuperShot = false;
            playerHasBall = false;
            pickupCooldown = 0;
            isAiming = false;
            TrajectoireBalle.Visibility = Visibility.Collapsed;
            UpdateBallVisuals();
        }

        private void TogglePause()
        {
            if (isPaused)
            {
                isPaused = false;
                canvasMenuPause.Visibility = Visibility.Collapsed;
                gameTimer.Start();
                this.Focus();
            }
            else
            {
                // En pause, on cache la visée pour faire propre
                isAiming = false;
                TrajectoireBalle.Visibility = Visibility.Collapsed;

                isPaused = true;
                canvasMenuPause.Visibility = Visibility.Visible;
                gameTimer.Stop();

                if (FinalScoreText != null)
                    FinalScoreText.Text = $"{playerScore} - {aiScore}";
            }
        }

        private void GameLoop(object sender, EventArgs e)
        {
            if (isPaused) return;

            if (pickupCooldown > 0) pickupCooldown--;

            // Définition des touches selon le mode
            bool inputLeft = (!useZQSD && Keyboard.IsKeyDown(Key.Left)) || (useZQSD && Keyboard.IsKeyDown(Key.Q));
            bool inputRight = (!useZQSD && Keyboard.IsKeyDown(Key.Right)) || (useZQSD && Keyboard.IsKeyDown(Key.D));
            bool inputJump = (!useZQSD && Keyboard.IsKeyDown(Key.Up)) || (useZQSD && Keyboard.IsKeyDown(Key.Z));

            // Calcul vitesse
            double currentSpeed = Keyboard.IsKeyDown(Key.LeftShift) ? (baseSpeed + sprintBonus) : baseSpeed;

            // Application des mouvements
            if (inputLeft)
            {
                MoveChar(-currentSpeed);
                ((ScaleTransform)Joueur.RenderTransform).ScaleX = -1;
            }

            if (inputRight)
            {
                MoveChar(currentSpeed);
                ((ScaleTransform)Joueur.RenderTransform).ScaleX = 1;
            }

            if (inputJump && !playerJumping)
            {
                playerVelY = jumpForce;
                playerJumping = true;
            }

            // Physique Joueur
            ApplyPhysics();

            // Physique Balle
            HandleBallLogic();

            // Collisions Panier
            CheckCollisions();

            // Particules
            UpdateParticles();

            // Mise à jour barre super
            if (SuperBar != null) SuperBar.Value = superCharge;
        }

        private void MoveChar(double amount)
        {
            double newLeft = Canvas.GetLeft(Joueur) + amount;
            if (newLeft > -50 && newLeft < WINDOW_WIDTH - 150)
                Canvas.SetLeft(Joueur, newLeft);
        }

        private void ApplyPhysics()
        {
            double top = Canvas.GetTop(Joueur) + playerVelY;
            playerVelY += GRAVITY;
            if (top + Joueur.Height >= GROUND_Y)
            {
                top = GROUND_Y - Joueur.Height;
                playerJumping = false;
                playerVelY = 0;
            }
            Canvas.SetTop(Joueur, top);
        }

        private void HandleBallLogic()
        {
            if (playerHasBall)
            {
                ballX = Canvas.GetLeft(Joueur) + (Joueur.Width / 2) - 40;
                ballY = Canvas.GetTop(Joueur) + (Joueur.Height / 3);
                ballVelX = 0;
                ballVelY = 0;
                isSuperShot = false;
            }
            else
            {
                ballX += ballVelX;
                ballY += ballVelY;
                ballVelY += GRAVITY * 0.8;
                ballAngle += ballVelX * 2;

                RotateTransform rotate = Balle.RenderTransform as RotateTransform;
                if (rotate == null)
                {
                    rotate = new RotateTransform();
                    Balle.RenderTransform = rotate;
                }
                rotate.Angle = ballAngle;

                // Rebond sol
                if (ballY + 80 >= GROUND_Y)
                {
                    ballY = GROUND_Y - 80;
                    ballVelY = -ballVelY * 0.6;
                    ballVelX *= 0.95;
                }

                // Si la balle sort de l'écran, on la remet au centre
                if (ballX > WINDOW_WIDTH + 100 || ballX < -100)
                    ResetBall();

                // Ramassage
                if (pickupCooldown == 0)
                {
                    Rect ballRect = new Rect(ballX, ballY, 80, 80);
                    Rect playerRect = new Rect(Canvas.GetLeft(Joueur) + 50, Canvas.GetTop(Joueur), 100, 300);
                    if (playerRect.IntersectsWith(ballRect))
                    {
                        playerHasBall = true;
                        isAiming = false;
                    }
                }
            }
            UpdateBallVisuals();
        }

        private void UpdateBallVisuals()
        {
            Balle.Opacity = (isSuperShot && DateTime.Now.Millisecond % 100 < 50) ? 0.5 : 1.0;
            Canvas.SetLeft(Balle, ballX);
            Canvas.SetTop(Balle, ballY);
        }

        private void CheckCollisions()
        {
            if (playerHasBall) return;
            Rect b = new Rect(ballX, ballY, 80, 80);

            // Panier Droite
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(collisionPanierDroit), Canvas.GetTop(collisionPanierDroit), 10, 100)))
                ballVelX = -ballVelX * 0.6;
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(collisionPanierGauche), Canvas.GetTop(collisionPanierGauche), 10, 100)))
                ballVelX = -ballVelX * 0.6;

            // Si on marque
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(zoneScorePanierDroit), Canvas.GetTop(zoneScorePanierDroit), 50, 10)) && ballVelY > 0)
            {
                // Juste des effets visuels, pas de score
                playerScore += 2;
                SpawnConfetti(Canvas.GetLeft(zoneScorePanierDroit), Canvas.GetTop(zoneScorePanierDroit));
                ResetBall();
            }

            if (b.IntersectsWith(new Rect(Canvas.GetLeft(zoneScorePanierGauche), Canvas.GetTop(zoneScorePanierGauche), 50, 10)) && ballVelY > 0)
            {
                aiScore += 2;
                SpawnConfetti(Canvas.GetLeft(zoneScorePanierGauche), Canvas.GetTop(zoneScorePanierGauche));
                ResetBall();
            }
            UpdateScore();
        }

        // --- GESTION SOURIS (TIR) ---
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isPaused) return;
            if (playerHasBall)
            {
                isAiming = true;
                TrajectoireBalle.Visibility = Visibility.Visible;
                TrajectoireBalle.Stroke = superCharge >= 100 ? Brushes.Red : Brushes.Yellow;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (isPaused || !isAiming || !playerHasBall) return;
            Point mousePos = e.GetPosition(canvasJeu);
            double velX = (mousePos.X - ballX) * 0.15;
            double velY = (mousePos.Y - ballY) * 0.15;

            if (velX > 35) velX = 35;
            if (velX < -35) velX = -35;
            if (velY > 35) velY = 35;
            if (velY < -35) velY = -35;

            // Dessin trajectoire
            PointCollection points = new PointCollection();
            for (int i = 0; i < 15; i++)
            {
                double t = i * 2;
                points.Add(new Point(ballX + velX * t, ballY + velY * t + 0.5 * (GRAVITY * 0.8) * t * t));
            }
            TrajectoireBalle.Points = points;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isPaused || !isAiming || !playerHasBall) return;
            isAiming = false;
            TrajectoireBalle.Visibility = Visibility.Collapsed;
            Point mousePos = e.GetPosition(canvasJeu);
            ballVelX = (mousePos.X - ballX) * 0.15;
            ballVelY = (mousePos.Y - ballY) * 0.15;

            if (superCharge >= 100)
            {
                isSuperShot = true;
                ballVelX = 25;
                ballVelY = -15;
                superCharge = 0;
            }
            playerHasBall = false;
            pickupCooldown = 30;
        }

        // --- EFFETS ---
        private void SpawnConfetti(double x, double y)
        {
            for (int i = 0; i < 20; i++)
            {
                Rectangle rect = new Rectangle { Width = 8, Height = 8 };
                byte r = (byte)rnd.Next(256);
                byte g = (byte)rnd.Next(256);
                byte b = (byte)rnd.Next(256);
                rect.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvasJeu.Children.Add(rect);
                particles.Add(new Particle
                {
                    Shape = rect,
                    VelX = rnd.NextDouble() * 10 - 5,
                    VelY = rnd.NextDouble() * 10 - 5,
                    Life = 60
                });
            }
        }

        private void UpdateParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.Life--;
                Canvas.SetLeft(p.Shape, Canvas.GetLeft(p.Shape) + p.VelX);
                Canvas.SetTop(p.Shape, Canvas.GetTop(p.Shape) + p.VelY);
                p.VelY += 0.5;
                if (p.Life <= 0)
                {
                    canvasJeu.Children.Remove(p.Shape);
                    particles.RemoveAt(i);
                }
            }
        }

        private void UpdateScore()
        {
            PlayerScoreText.Text = playerScore.ToString();
            EnemyScoreText.Text = aiScore.ToString();

            if (FinalScoreText == null)
            {
                FinalScoreText.Text = "00 - 00";
            }
            else
            {
                FinalScoreText.Text = $"{playerScore} - {aiScore}";
            }
        }

        // --- BOUTONS ---
        private void RetourMenu_Click(object sender, RoutedEventArgs e)
        {
            this.Content = new UCMenuPrincipal();
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            gameTimer.Stop();
            canvasMenuPause.Visibility = Visibility.Visible;
            if (FinalScoreText != null) FinalScoreText.Text = $"{playerScore} - {aiScore}";
        }

        private void buttonQuitter_Click(object sender, RoutedEventArgs e)
        {
            this.Content = new UCMenuPrincipal();
        }

        private void buttonReprendre_Click(object sender, RoutedEventArgs e)
        {
            gameTimer.Start();
            canvasMenuPause.Visibility = Visibility.Collapsed;
            this.Focus();
        }

        private void ButtonRetourOptions_Click(object sender, RoutedEventArgs e)
        {
            canvasMenuPause.Visibility = Visibility.Visible;
            MenuOptions.Visibility = Visibility.Collapsed;
            this.Focus();
        }

        private void ButtonOptions_Click(Object sender, RoutedEventArgs e)
        {
            MenuOptions.Visibility = Visibility.Visible;
        }

        private void ButtonValider_Click(object sender, RoutedEventArgs e)
        {
            if (ChoixControles != null) useZQSD = (ChoixControles.SelectedIndex == 1);

            MenuOptions.Visibility = Visibility.Collapsed;
            if (isPaused) canvasMenuPause.Visibility = Visibility.Visible;
            this.Focus();
        }

        private void GameCanvas_Click(object sender, MouseButtonEventArgs e) => this.Focus();

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) TogglePause();
        }

        private void OnKeyUp(object sender, KeyEventArgs e) { }
    }
}