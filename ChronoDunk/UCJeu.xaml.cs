using Projet_sae;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ChronoDunk
{
    public partial class UCJeu : UserControl
    {
        // --- TIMERS ---
        DispatcherTimer gameTimer = new DispatcherTimer();
        DispatcherTimer matchClock = new DispatcherTimer();
        DispatcherTimer countdownTimer = new DispatcherTimer();

        int currentGameMode; // 1 = Solo, 2 = Multijoueur

        // --- CONSTANTES ---
        const double GROUND_Y = 550;
        const double GRAVITY = 1.5;

        // --- ETAT DU JEU ---
        bool isGameActive = false; // Bloqué au départ pour le décompte
        bool isPaused = false;
        int matchTime = 60;
        int countdownValue = 3;

        // --- JOUEUR 1 (STATS DYNAMIQUES) ---
        double baseSpeed = 12;      // Sera remplacé par les stats du perso
        double jumpForce = -28;     // Sera remplacé par les stats du perso
        double sprintBonus = 8;     // Bonus quand on court avec Maj

        double playerVelY = 0;
        bool playerJumping = false;
        bool playerHasBall = false;
        int playerScore = 0;
        int superCharge = 0;

        // --- ENNEMI (IA / P2) ---
        double enemySpeed = 10.5;
        double enemyVelY = 0;
        bool enemyJumping = false;
        bool enemyHasBall = false;
        int enemyScore = 0;

        // Cerveau IA
        int aiShootTimer = 0;
        int aiStealTimer = 0;
        int aiActionTimer = 0;

        // --- BALLON ---
        double ballX, ballY, ballVelX, ballVelY;
        double ballAngle = 0;
        int pickupCooldown = 0; // Sécurité anti-spam ramassage
        bool isSuperShot = false;
        bool isAiming = false;

        // --- EFFETS VISUELS (Juice) ---
        int shakeDuration = 0;
        Random rnd = new Random();
        List<Particle> particles = new List<Particle>();

        // Classe interne pour les confettis
        class Particle
        {
            public Rectangle Shape;
            public double VelX, VelY;
            public int Life;
        }

        // --- CONSTRUCTEUR ---
        public UCJeu(int gameMode, CharacterStats p1Stats)
        {
            InitializeComponent();
            this.currentGameMode = gameMode;

            // 1. APPLIQUER LES STATS DU PERSONNAGE CHOISI
            if (p1Stats != null)
            {
                baseSpeed = p1Stats.Speed;
                jumpForce = p1Stats.JumpForce;
                try
                {
                    Player.Source = new BitmapImage(new Uri(p1Stats.ImagePath, UriKind.Relative));
                }
                catch { }
            }

            // Équilibrage Vitesse Ennemi
            if (currentGameMode == 1) enemySpeed = 9.5; // IA
            if (currentGameMode == 2) enemySpeed = baseSpeed; // P2 (Humain) a les mêmes stats de base

            // Focus clavier + Lancement
            this.Loaded += (s, e) => { this.Focus(); StartCountdown(); };

            // Boucle Physique (60 FPS)
            gameTimer.Interval = TimeSpan.FromMilliseconds(16);
            gameTimer.Tick += GameLoop;
            gameTimer.Start();

            // Boucle Chrono (1 sec)
            matchClock.Interval = TimeSpan.FromSeconds(1);
            matchClock.Tick += MatchClock_Tick;

            // Boucle Décompte (1 sec)
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += Countdown_Tick;
        }

        // =========================================================
        // 1. GESTION DU DÉROULEMENT (Countdown, Reset, Pause)
        // =========================================================

        private void StartCountdown()
        {
            isGameActive = false;
            matchClock.Stop();

            ResetPositions(false); // Reset sans délai pour le placement initial

            countdownValue = 3;
            CountdownText.Text = "3";
            CountdownText.Visibility = Visibility.Visible;
            countdownTimer.Start();
        }

        private void Countdown_Tick(object sender, EventArgs e)
        {
            countdownValue--;
            if (countdownValue > 0)
            {
                CountdownText.Text = countdownValue.ToString();
                CountdownText.FontSize = 100;
            }
            else if (countdownValue == 0)
            {
                CountdownText.Text = "GO!";
                CountdownText.Foreground = Brushes.Lime;
            }
            else
            {
                countdownTimer.Stop();
                CountdownText.Visibility = Visibility.Collapsed;
                CountdownText.Foreground = Brushes.Yellow;
                isGameActive = true; // LE MATCH COMMENCE
                matchClock.Start();
            }
        }

        // Appelé après un but ou au début
        private void ResetPositions(bool withDelay = true)
        {
            // Balle au plafond (au milieu)
            ballX = 400 - (Ball.Width / 2);
            ballY = 50;
            ballVelX = 0; ballVelY = 0; ballAngle = 0;
            isSuperShot = false;

            playerHasBall = false; enemyHasBall = false;

            // Si on vient de marquer, on empêche de toucher la balle pendant 1 sec (60 frames)
            pickupCooldown = withDelay ? 60 : 0;

            aiShootTimer = 0; aiStealTimer = 0; aiActionTimer = 0;
            isAiming = false;
            TrajectoryLine.Visibility = Visibility.Collapsed;

            // Retour aux camps
            Canvas.SetLeft(Player, 150); SetFacing(Player, true);
            Canvas.SetLeft(Enemy, 550); SetFacing(Enemy, false);

            UpdateBallVisuals();
        }

        private void TogglePause()
        {
            if (countdownValue > 0 || !isGameActive) return;

            if (isPaused)
            {
                isPaused = false;
                PauseMenu.Visibility = Visibility.Collapsed;
                gameTimer.Start(); matchClock.Start();
                this.Focus();
            }
            else
            {
                isPaused = true;
                PauseMenu.Visibility = Visibility.Visible;
                gameTimer.Stop(); matchClock.Stop();
            }
        }

        // =========================================================
        // 2. BOUCLE DE JEU PRINCIPALE (Game Loop)
        // =========================================================
        private void GameLoop(object sender, EventArgs e)
        {
            // Les effets visuels tournent toujours
            UpdateParticles();
            UpdateScreenShake();

            if (!isGameActive || isPaused) return;

            if (pickupCooldown > 0) pickupCooldown--;

            // INPUTS
            HandlePlayer1Input();
            if (currentGameMode == 2) HandlePlayer2Input(); else UpdateAI();

            // PHYSIQUE
            ApplyPhysics(Player, ref playerVelY, ref playerJumping);
            ApplyPhysics(Enemy, ref enemyVelY, ref enemyJumping);

            // LOGIQUE
            HandleBallLogic();
            CheckCollisions();

            // UI
            SuperBar.Value = superCharge;
        }

        // =========================================================
        // 3. EFFETS VISUELS (PARTICULES & SHAKE)
        // =========================================================
        private void SpawnConfetti(double x, double y)
        {
            for (int i = 0; i < 30; i++)
            {
                Rectangle rect = new Rectangle { Width = 8, Height = 8 };
                byte r = (byte)rnd.Next(256); byte g = (byte)rnd.Next(256); byte b = (byte)rnd.Next(256);
                rect.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
                Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y);
                GameCanvas.Children.Add(rect);

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
                p.VelY += 0.5; // Gravité des confettis

                if (p.Life < 20) p.Shape.Opacity = p.Life / 20.0;

                if (p.Life <= 0)
                {
                    GameCanvas.Children.Remove(p.Shape);
                    particles.RemoveAt(i);
                }
            }
        }

        private void ShakeScreen(int duration) { shakeDuration = duration; }
        private void UpdateScreenShake()
        {
            if (shakeDuration > 0)
            {
                CanvasShake.X = rnd.Next(-10, 11);
                CanvasShake.Y = rnd.Next(-10, 11);
                shakeDuration--;
            }
            else { CanvasShake.X = 0; CanvasShake.Y = 0; }
        }

        // =========================================================
        // 4. CONTRÔLES & IA
        // =========================================================
        private void HandlePlayer1Input()
        {
            // Vitesse dynamique basée sur les stats choisies + sprint
            double currentSpeed = Keyboard.IsKeyDown(Key.LeftShift) ? (baseSpeed + sprintBonus) : baseSpeed;

            if (Keyboard.IsKeyDown(Key.Left)) { MoveChar(Player, -currentSpeed); SetFacing(Player, false); }
            if (Keyboard.IsKeyDown(Key.Right)) { MoveChar(Player, currentSpeed); SetFacing(Player, true); }

            // Saut dynamique
            if (Keyboard.IsKeyDown(Key.Up) && !playerJumping) { playerVelY = jumpForce; playerJumping = true; }

            if (Keyboard.IsKeyDown(Key.Down)) AttemptSteal(true);
        }

        private void HandlePlayer2Input()
        {
            double currentSpeed = Keyboard.IsKeyDown(Key.RightShift) ? (baseSpeed + 8) : baseSpeed;
            if (Keyboard.IsKeyDown(Key.Q) || Keyboard.IsKeyDown(Key.A)) { MoveChar(Enemy, -currentSpeed); SetFacing(Enemy, false); }
            if (Keyboard.IsKeyDown(Key.D)) { MoveChar(Enemy, currentSpeed); SetFacing(Enemy, true); }
            if ((Keyboard.IsKeyDown(Key.Z) || Keyboard.IsKeyDown(Key.W)) && !enemyJumping) { enemyVelY = jumpForce; enemyJumping = true; }
            if (Keyboard.IsKeyDown(Key.S)) AttemptSteal(false);
        }

        private void MoveChar(UIElement person, double amount)
        {
            double newLeft = Canvas.GetLeft(person) + amount;
            if (newLeft > -80 && newLeft < 700) Canvas.SetLeft(person, newLeft);
        }

        private void AttemptSteal(bool isP1)
        {
            if (pickupCooldown > 0) return;
            if (isP1 && enemyHasBall && Math.Abs(Canvas.GetLeft(Player) - Canvas.GetLeft(Enemy)) < 100)
            { enemyHasBall = false; playerHasBall = true; pickupCooldown = 15; }
            else if (!isP1 && playerHasBall && Math.Abs(Canvas.GetLeft(Player) - Canvas.GetLeft(Enemy)) < 100)
            { playerHasBall = false; enemyHasBall = true; pickupCooldown = 15; }
        }

        // --- CERVEAU IA (EQUILIBRÉ) ---
        private void UpdateAI()
        {
            double enemyX = Canvas.GetLeft(Enemy);
            aiActionTimer++;

            // ATTAQUE
            if (enemyHasBall)
            {
                if (enemyX > 200) { MoveChar(Enemy, -enemySpeed); SetFacing(Enemy, false); }
                else { aiShootTimer++; if (aiShootTimer > 40) EnemySmartShoot(); }
            }
            // DÉFENSE
            else
            {
                aiShootTimer = 0;
                double targetX = playerHasBall ? Canvas.GetLeft(Player) : ballX;

                if (targetX > enemyX + 20) { MoveChar(Enemy, enemySpeed); SetFacing(Enemy, true); }
                else if (targetX < enemyX - 20) { MoveChar(Enemy, -enemySpeed); SetFacing(Enemy, false); }

                // Vol (Rare)
                if (playerHasBall && Math.Abs(enemyX - targetX) < 60 && aiActionTimer > 90)
                {
                    if (rnd.Next(0, 3) == 0) AttemptSteal(false);
                    aiActionTimer = 0;
                }

                // Saut (Seulement si nécessaire)
                if (!enemyHasBall && !playerHasBall && ballY < 350 && Math.Abs(ballX - enemyX) < 60 && !enemyJumping)
                {
                    if (rnd.Next(0, 10) == 0) { enemyVelY = -28; enemyJumping = true; }
                }
            }
        }

        private void EnemySmartShoot()
        {
            enemyHasBall = false;
            double targetX = 35; double currentX = Canvas.GetLeft(Enemy); double dist = currentX - targetX;
            ballVelY = -32;
            ballVelX = -(dist / 26.0) + (rnd.NextDouble() * 3 - 1.5); // Petite marge d'erreur
            pickupCooldown = 20;
        }

        // =========================================================
        // 5. PHYSIQUE ET BALLON
        // =========================================================
        private void ApplyPhysics(UIElement charObj, ref double velY, ref bool isJumping)
        {
            double top = Canvas.GetTop(charObj) + velY;
            velY += GRAVITY;

            if (top + ((FrameworkElement)charObj).Height >= GROUND_Y)
            {
                top = GROUND_Y - ((FrameworkElement)charObj).Height;
                isJumping = false;
                velY = 0;
            }
            Canvas.SetTop(charObj, top);
        }

        private void HandleBallLogic()
        {
            if (playerHasBall)
            {
                ballX = Canvas.GetLeft(Player) + (Player.Width / 2) - (Ball.Width / 2);
                ballY = Canvas.GetTop(Player) + (Player.Height / 3);
                ballVelX = 0; ballVelY = 0; isSuperShot = false;
            }
            else if (enemyHasBall)
            {
                ballX = Canvas.GetLeft(Enemy) + (Enemy.Width / 2) - (Ball.Width / 2);
                ballY = Canvas.GetTop(Enemy) + (Enemy.Height / 3);
                ballVelX = 0; ballVelY = 0;
            }
            else
            {
                // Balle Libre
                ballX += ballVelX;
                ballY += ballVelY;
                ballVelY += GRAVITY * 0.8;
                ballAngle += ballVelX * 2;

                RotateTransform rotate = Ball.RenderTransform as RotateTransform;
                if (rotate == null) { rotate = new RotateTransform(); Ball.RenderTransform = rotate; }
                rotate.Angle = ballAngle;

                // Rebond Sol
                if (ballY + Ball.Height >= GROUND_Y)
                {
                    ballY = GROUND_Y - Ball.Height;
                    ballVelY = -ballVelY * 0.6;
                    ballVelX *= 0.95;
                }

                // Sécurité hors écran
                if (ballX > 900 || ballX < -150) ResetPositions(true);

                // RAMASSAGE (Bloqué si Cooldown > 0)
                if (pickupCooldown == 0)
                {
                    Rect ballRect = new Rect(ballX, ballY, Ball.Width, Ball.Height);
                    if (new Rect(Canvas.GetLeft(Player) - 10, Canvas.GetTop(Player), Player.Width + 20, Player.Height).IntersectsWith(ballRect))
                    { playerHasBall = true; isAiming = false; }
                    if (new Rect(Canvas.GetLeft(Enemy) - 10, Canvas.GetTop(Enemy), Enemy.Width + 20, Enemy.Height).IntersectsWith(ballRect))
                    { enemyHasBall = true; }
                }
            }

            UpdateBallVisuals();
        }

        private void UpdateBallVisuals()
        {
            Ball.Opacity = (isSuperShot && DateTime.Now.Millisecond % 100 < 50) ? 0.5 : 1.0;
            Canvas.SetLeft(Ball, ballX);
            Canvas.SetTop(Ball, ballY);
        }

        // =========================================================
        // 6. TIR SOURIS
        // =========================================================
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isPaused) return;
            if (playerHasBall || enemyHasBall)
            {
                isAiming = true;
                TrajectoryLine.Visibility = Visibility.Visible;
                TrajectoryLine.Stroke = superCharge >= 100 ? Brushes.Red : Brushes.Yellow;
            }
            this.Focus();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (isPaused) return;
            if (isAiming && (playerHasBall || enemyHasBall))
            {
                Point mousePos = e.GetPosition(GameCanvas);
                double velX = (mousePos.X - ballX) * 0.15;
                double velY = (mousePos.Y - ballY) * 0.15;
                if (velX > 35) velX = 35; if (velX < -35) velX = -35;
                if (velY > 35) velY = 35; if (velY < -35) velY = -35;
                DrawTrajectory(ballX, ballY, velX, velY);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isPaused) return;
            if (isAiming && (playerHasBall || enemyHasBall))
            {
                isAiming = false;
                TrajectoryLine.Visibility = Visibility.Collapsed;
                Point mousePos = e.GetPosition(GameCanvas);
                ballVelX = (mousePos.X - ballX) * 0.15;
                ballVelY = (mousePos.Y - ballY) * 0.15;

                if (superCharge >= 100)
                {
                    isSuperShot = true;
                    ballVelX = 25; ballVelY = -15;
                    superCharge = 0;
                }

                playerHasBall = false; enemyHasBall = false;
                pickupCooldown = 30; // Délai après tir
            }
        }

        private void DrawTrajectory(double startX, double startY, double velX, double velY)
        {
            PointCollection points = new PointCollection();
            for (int i = 0; i < 15; i++)
            {
                double t = i * 2;
                double x = startX + velX * t;
                double y = startY + velY * t + 0.5 * (GRAVITY * 0.8) * t * t;
                points.Add(new Point(x, y));
            }
            TrajectoryLine.Points = points;
        }

        // =========================================================
        // 7. COLLISIONS & SCORE
        // =========================================================
        private void CheckCollisions()
        {
            if (playerHasBall || enemyHasBall) return;
            Rect b = new Rect(ballX, ballY, Ball.Width, Ball.Height);

            // DROITE (P1)
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(HitboxBackboardRight), Canvas.GetTop(HitboxBackboardRight), 10, 100))) ballVelX = -ballVelX * 0.6;
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(HitboxHoopRight), Canvas.GetTop(HitboxHoopRight), 50, 10)) && ballVelY > 0)
            {
                playerScore += (isSuperShot ? 3 : 2); superCharge += 30; if (superCharge > 100) superCharge = 100;
                SpawnConfetti(Canvas.GetLeft(HitboxHoopRight), Canvas.GetTop(HitboxHoopRight));
                if (isSuperShot) ShakeScreen(20);
                UpdateScore();
                ResetPositions(true); // RESET SIMPLE (PAS DE COMPTE A REBOURS)
            }

            // GAUCHE (P2/IA)
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(HitboxBackboardLeft), Canvas.GetTop(HitboxBackboardLeft), 10, 100))) ballVelX = -ballVelX * 0.6;
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(HitboxHoopLeft), Canvas.GetTop(HitboxHoopLeft), 50, 10)) && ballVelY > 0)
            {
                enemyScore += 2; superCharge += 10; if (superCharge > 100) superCharge = 100;
                SpawnConfetti(Canvas.GetLeft(HitboxHoopLeft), Canvas.GetTop(HitboxHoopLeft));
                UpdateScore();
                ResetPositions(true); // RESET SIMPLE (PAS DE COMPTE A REBOURS)
            }
        }

        private void UpdateScore()
        {
            PlayerScoreText.Text = playerScore.ToString();
            EnemyScoreText.Text = enemyScore.ToString();
        }

        private void MatchClock_Tick(object sender, EventArgs e)
        {
            if (!isGameActive || isPaused) return;
            matchTime--;
            TimerText.Text = matchTime.ToString("00");
            if (matchTime <= 0) EndGame();
        }

        private void EndGame()
        {
            isGameActive = false;
            gameTimer.Stop(); matchClock.Stop();
            GameOverScreen.Visibility = Visibility.Visible;
            string winner = playerScore > enemyScore ? "VICTOIRE !" : (playerScore == enemyScore ? "ÉGALITÉ" : "DÉFAITE...");
            FinalScoreText.Text = $"{playerScore} - {enemyScore}\n{winner}";
        }

        // =========================================================
        // 8. BOUTONS & UTILITAIRES
        // =========================================================
        private void Resume_Click(object sender, RoutedEventArgs e) => TogglePause();
        private void QuitGame_Click(object sender, RoutedEventArgs e)
        {
            playerScore = 0; enemyScore = 0; superCharge = 0; matchTime = 60;
            isPaused = false; PauseMenu.Visibility = Visibility.Collapsed; GameOverScreen.Visibility = Visibility.Collapsed;
            UpdateScore(); StartCountdown(); gameTimer.Start();
        }
        private void QuitToMenu_Click(object sender, RoutedEventArgs e)
        {
            // 1. On arrête les timers
            gameTimer.Stop();
            matchClock.Stop();

            // 2. On crée une nouvelle instance du menu
            UCMenuPrincipal menu = new UCMenuPrincipal();

            // 3. On remplace l'écran actuel (le jeu) par le menu
            ((MainWindow)Application.Current.MainWindow).Content = menu;
        } 
        private void GameCanvas_Click(object sender, MouseButtonEventArgs e) { this.Focus(); }

        private void buttonQuitter_Click(object sender, RoutedEventArgs e)
        {
            this.Content = new UCMenuPrincipal();
        }

        private void buttonReprendre_Click(object sender, RoutedEventArgs e)
        {
            gameTimer.Start();

            matchClock.Start();

            canvasMenuPause.Visibility = Visibility.Collapsed;

            this.Focus();
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            gameTimer.Stop();

            matchClock.Stop();

            canvasMenuPause.Visibility = Visibility.Visible;
        }

        private void SetFacing(Image t, bool r) { ((ScaleTransform)t.RenderTransform).ScaleX = r ? 1 : -1; }
        private void OnKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) TogglePause(); if (isPaused) return; if (e.Key == Key.Up && !playerJumping) { playerVelY = jumpForce; playerJumping = true; } }
        private void OnKeyUp(object sender, KeyEventArgs e) { }
    }
}