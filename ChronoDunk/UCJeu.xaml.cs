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
        // --- CONSTANTES DE JEU ---
        const double WINDOW_WIDTH = 1280;
        const double GROUND_Y = 650;
        const double GRAVITY = 1.5;
        const double HOOP_LEFT_X = 238;   // Position panier gauche
        const double HOOP_RIGHT_X = 1195; // Position panier droit
        const double HOOP_Y = 180;        // Hauteur panier

        // --- TIMERS ---
        DispatcherTimer gameTimer = new DispatcherTimer();
        DispatcherTimer matchClock = new DispatcherTimer();
        DispatcherTimer countdownTimer = new DispatcherTimer();

        // --- PARAMETRES ---
        int currentGameMode;
        bool useZQSD = false;

        // --- ETAT DU JEU ---
        bool isGameActive = false;
        bool isPaused = false;
        int matchTime = 30;
        int countdownValue = 3;

        // --- JOUEUR 1 ---
        double baseSpeed = 12;
        double jumpForce = -28;
        double sprintBonus = 8;
        double playerVelY = 0;
        bool playerJumping = false;
        bool playerHasBall = false;
        int playerScore = 0;
        int superCharge = 0;

        // --- ENNEMI (IA) ---
        double enemySpeed = 10.5;
        double enemyVelY = 0;
        bool enemyJumping = false;
        bool enemyHasBall = false;
        int enemyScore = 0;

        // --- IA VARIABLES ---
        int aiReactionSpeed = 40;
        double aiErrorMargin = 1.5;
        int aiStealAggro = 3;
        int aiShootTimer = 0;
        int aiActionTimer = 0;
        int aiJumpCooldown = 0;
        double aiShootingRange = 300;

        // --- BALLON ---
        double balleX, balleY, balleVelX, balleVelY;
        double ballAngle = 0;
        int pickupCooldown = 0;
        bool isSuperShot = false;
        bool isAiming = false;

        // --- EFFETS ---
        int shakeDuration = 0;
        Random rnd = new Random();
        List<Particle> particles = new List<Particle>();

        class Particle { public Rectangle Shape; public double VelX, VelY; public int Life; }

        public UCJeu(int gameMode, CharacterStats p1Stats)
        {
            InitializeComponent();
            this.currentGameMode = gameMode;

            // 1. Initialisation Joueur
            if (p1Stats != null)
            {
                baseSpeed = p1Stats.Speed;
                jumpForce = p1Stats.JumpForce;
                try { Player.Source = new BitmapImage(new Uri(p1Stats.ImagePath, UriKind.Relative)); } catch { }
            }

            // 2. Initialisation Difficulté
            int defaultLevel = 1;
            if (DifficulteIA != null) DifficulteIA.SelectedIndex = defaultLevel;
            ApplyDifficulty(defaultLevel);

            if (currentGameMode == 2) enemySpeed = baseSpeed;

            // 3. Events
            this.Loaded += (s, e) => {
                this.Focus();
                StartCountdown();
            };

            gameTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            gameTimer.Tick += GameLoop;

            matchClock.Interval = TimeSpan.FromSeconds(1);
            matchClock.Tick += MatchClock_Tick;

            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += Countdown_Tick;
        }

        // =========================================================
        // GESTION DIFFICULTÉ
        // =========================================================
        private void ComboDifficulty_SelectionChanged(object sender, SelectionChangedEventArgs e) { } // Géré par le bouton Valider

        private void ApplyDifficulty(int level)
        {
            if (currentGameMode == 2) return;

            switch (level)
            {
                case 0: // FACILE
                    enemySpeed = 7.0; aiReactionSpeed = 60; aiErrorMargin = 4.0; aiStealAggro = 150; aiShootingRange = 200;
                    break;
                case 1: // NORMAL
                    enemySpeed = 10.0; aiReactionSpeed = 30; aiErrorMargin = 1.5; aiStealAggro = 60; aiShootingRange = 400;
                    break;
                case 2: // NBA STAR
                    enemySpeed = 15; aiReactionSpeed = 5; aiErrorMargin = 0; aiStealAggro = 20; aiShootingRange = 650;
                    break;
            }
        }

        // =========================================================
        // INTELLIGENCE ARTIFICIELLE (Optimisée)
        // =========================================================
        private void UpdateAI()
        {
            double enemyX = Canvas.GetLeft(Enemy);
            double enemyY = Canvas.GetTop(Enemy);
            double playerX = Canvas.GetLeft(Player);

            aiActionTimer++;
            if (aiJumpCooldown > 0) aiJumpCooldown--;

            // 1. ATTAQUE
            if (enemyHasBall)
            {
                double distanceToHoop = enemyX - HOOP_LEFT_X;

                if (distanceToHoop < 150) // Trop près
                {
                    MoveChar(Enemy, enemySpeed);
                    SetFacing(Enemy, false);
                }
                else if (distanceToHoop <= aiShootingRange) // Tir
                {
                    aiShootTimer++;
                    if (aiShootTimer > aiReactionSpeed)
                    {
                        EnemySmartShoot();
                        aiShootTimer = 0;
                    }
                }
                else // Avance
                {
                    MoveChar(Enemy, -enemySpeed);
                    SetFacing(Enemy, false);
                }
            }
            // 2. BALLE LIBRE
            else if (!playerHasBall)
            {
                if (balleX > enemyX + 10) { MoveChar(Enemy, enemySpeed); SetFacing(Enemy, true); }
                else if (balleX < enemyX - 10) { MoveChar(Enemy, -enemySpeed); SetFacing(Enemy, false); }

                // Saut pour attraper la balle
                if (balleY < enemyY - 50 && Math.Abs(balleX - enemyX) < 80 && !enemyJumping && aiJumpCooldown == 0)
                {
                    enemyVelY = -28; enemyJumping = true; aiJumpCooldown = 60;
                }
            }
            // 3. DÉFENSE
            else
            {
                aiShootTimer = 0;
                double targetPos = (aiActionTimer > aiStealAggro) ? playerX : playerX + 150;

                if (targetPos > enemyX + 20) { MoveChar(Enemy, enemySpeed); SetFacing(Enemy, true); }
                else if (targetPos < enemyX - 20) { MoveChar(Enemy, -enemySpeed); SetFacing(Enemy, false); }

                // Tentative de vol
                if (Math.Abs(enemyX - playerX) < 80 && aiActionTimer > aiStealAggro)
                {
                    AttemptSteal(false);
                    aiActionTimer = 0;
                }

                // Contre (Block)
                if (playerJumping && !enemyJumping && Math.Abs(enemyX - playerX) < 100 && aiJumpCooldown == 0)
                {
                    if (rnd.Next(0, 10) > 1) { enemyVelY = -28; enemyJumping = true; aiJumpCooldown = 40; }
                }
            }
        }

        private void EnemySmartShoot()
        {
            enemyHasBall = false;
            double currentX = Canvas.GetLeft(Enemy);
            double distance = currentX - HOOP_LEFT_X;
            double flightTime = (distance > 450) ? 43.0 : 32.0;

            balleVelY = (distance > 450) ? -32 : -24;

            double perfectVelX = -(distance / flightTime);
            double randomError = (rnd.NextDouble() * (aiErrorMargin * 2)) - aiErrorMargin;
            balleVelX = perfectVelX + randomError;

            if (!enemyJumping) { enemyVelY = -15; enemyJumping = true; }
            pickupCooldown = 20;
        }

        // =========================================================
        // LOGIQUE JEU
        // =========================================================
        private void StartCountdown()
        {
            isGameActive = false; matchClock.Stop(); ResetPositions(false);
            countdownValue = 3; CountdownText.Text = "3";
            CountdownText.Visibility = Visibility.Visible;
            CountdownText.Foreground = Brushes.Yellow;
            CountdownText.FontSize = 100;
            countdownTimer.Start();
        }

        private void Countdown_Tick(object sender, EventArgs e)
        {
            countdownValue--;
            if (countdownValue > 0) CountdownText.Text = countdownValue.ToString();
            else if (countdownValue == 0) { CountdownText.Text = "GO!"; CountdownText.Foreground = Brushes.Lime; }
            else
            {
                countdownTimer.Stop();
                CountdownText.Visibility = Visibility.Collapsed;
                isGameActive = true;
                gameTimer.Start();
                matchClock.Start();
                this.Focus();
            }
        }

        private void ResetPositions(bool withDelay = true)
        {
            balleX = (WINDOW_WIDTH / 2) - (Balle.Width / 2); balleY = 100;
            balleVelX = 0; balleVelY = 0; ballAngle = 0; isSuperShot = false;
            playerHasBall = false; enemyHasBall = false;
            pickupCooldown = withDelay ? 60 : 0;
            aiShootTimer = 0; aiActionTimer = 0; isAiming = false;

            if (TrajectoireBalle != null) TrajectoireBalle.Visibility = Visibility.Collapsed;

            Canvas.SetLeft(Player, 200); SetFacing(Player, true);
            Canvas.SetLeft(Enemy, WINDOW_WIDTH - 200 - Enemy.Width); SetFacing(Enemy, false);
            UpdateBallVisuals();
        }

        private void GameLoop(object sender, EventArgs e)
        {
            UpdateParticles(); UpdateScreenShake();
            if (!isGameActive || isPaused) return;

            if (pickupCooldown > 0) pickupCooldown--;

            HandlePlayer1Input();
            if (currentGameMode == 2) HandlePlayer2Input(); else UpdateAI();

            ApplyPhysics(Player, ref playerVelY, ref playerJumping);
            ApplyPhysics(Enemy, ref enemyVelY, ref enemyJumping);

            HandleBallLogic();
            CheckCollisions();
            SuperBar.Value = superCharge;
        }

        // --- GESTION INPUT ---
        private void HandlePlayer1Input()
        {
            double currentSpeed = Keyboard.IsKeyDown(Key.LeftShift) ? (baseSpeed + sprintBonus) : baseSpeed;
            Key kL = useZQSD ? Key.Q : Key.Left;
            Key kR = useZQSD ? Key.D : Key.Right;
            Key kU = useZQSD ? Key.Z : Key.Up;
            Key kS = useZQSD ? Key.S : Key.Down;

            if (Keyboard.IsKeyDown(kL)) { MoveChar(Player, -currentSpeed); SetFacing(Player, false); }
            if (Keyboard.IsKeyDown(kR)) { MoveChar(Player, currentSpeed); SetFacing(Player, true); }
            if (Keyboard.IsKeyDown(kU) && !playerJumping) { playerVelY = jumpForce; playerJumping = true; }
            if (Keyboard.IsKeyDown(kS)) AttemptSteal(true);
        }

        private void HandlePlayer2Input()
        {
            double currentSpeed = Keyboard.IsKeyDown(Key.RightShift) ? (baseSpeed + 8) : baseSpeed;
            if (Keyboard.IsKeyDown(Key.Q) || Keyboard.IsKeyDown(Key.A)) { MoveChar(Enemy, -currentSpeed); SetFacing(Enemy, false); }
            if (Keyboard.IsKeyDown(Key.D)) { MoveChar(Enemy, currentSpeed); SetFacing(Enemy, true); }
            if ((Keyboard.IsKeyDown(Key.Z) || Keyboard.IsKeyDown(Key.W)) && !enemyJumping) { enemyVelY = jumpForce; enemyJumping = true; }
            if (Keyboard.IsKeyDown(Key.S)) AttemptSteal(false);
        }

        // --- PHYSIQUE & MOUVEMENTS ---
        private void MoveChar(UIElement person, double amount)
        {
            double newLeft = Canvas.GetLeft(person) + amount;
            if (newLeft > -50 && newLeft < WINDOW_WIDTH - 150) Canvas.SetLeft(person, newLeft);
        }

        // CORRECTIF CRASH : SetFacing sécurisé
        private void SetFacing(Image t, bool r)
        {
            if (t.RenderTransform is ScaleTransform st) st.ScaleX = r ? 1 : -1;
            else t.RenderTransform = new ScaleTransform(r ? 1 : -1, 1);
        }

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

        private void AttemptSteal(bool isP1)
        {
            if (pickupCooldown > 0) return;
            double pX = Canvas.GetLeft(Player);
            double eX = Canvas.GetLeft(Enemy);

            if (isP1 && enemyHasBall && Math.Abs(pX - eX) < 100)
            {
                enemyHasBall = false; playerHasBall = true; pickupCooldown = 15;
                isAiming = false; TrajectoireBalle.Visibility = Visibility.Collapsed;
            }
            else if (!isP1 && playerHasBall && Math.Abs(pX - eX) < 100)
            {
                playerHasBall = false; enemyHasBall = true; pickupCooldown = 15;
                isAiming = false; TrajectoireBalle.Visibility = Visibility.Collapsed;
            }
        }

        // --- BALLON LOGIC ---
        private void HandleBallLogic()
        {
            if (playerHasBall)
            {
                balleX = Canvas.GetLeft(Player) + (Player.Width / 2) - (Balle.Width / 2);
                balleY = Canvas.GetTop(Player) + (Player.Height / 3);
                balleVelX = 0; balleVelY = 0; isSuperShot = false;
            }
            else if (enemyHasBall)
            {
                balleX = Canvas.GetLeft(Enemy) + (Enemy.Width / 2) - (Balle.Width / 2);
                balleY = Canvas.GetTop(Enemy) + (Enemy.Height / 3);
                balleVelX = 0; balleVelY = 0;
            }
            else
            {
                balleX += balleVelX;
                balleY += balleVelY;
                balleVelY += GRAVITY * 0.8;
                ballAngle += balleVelX * 2;

                // CORRECTIF CRASH : RotateTransform sécurisé
                RotateTransform rt = Balle.RenderTransform as RotateTransform;
                if (rt == null) { rt = new RotateTransform(); Balle.RenderTransform = rt; }
                rt.Angle = ballAngle;

                if (balleY + Balle.Height >= GROUND_Y)
                {
                    balleY = GROUND_Y - Balle.Height;
                    balleVelY = -balleVelY * 0.6;
                    balleVelX *= 0.95;
                }

                if (balleX > WINDOW_WIDTH + 100 || balleX < -150) ResetPositions(true);

                if (pickupCooldown == 0)
                {
                    Rect bRect = new Rect(balleX, balleY, Balle.Width, Balle.Height);
                    if (new Rect(Canvas.GetLeft(Player) - 10, Canvas.GetTop(Player), Player.Width + 20, Player.Height).IntersectsWith(bRect))
                    { playerHasBall = true; isAiming = false; }

                    if (new Rect(Canvas.GetLeft(Enemy) - 10, Canvas.GetTop(Enemy), Enemy.Width + 20, Enemy.Height).IntersectsWith(bRect))
                    { enemyHasBall = true; }
                }
            }
            UpdateBallVisuals();
        }

        private void UpdateBallVisuals()
        {
            Balle.Opacity = (isSuperShot && DateTime.Now.Millisecond % 100 < 50) ? 0.5 : 1.0;
            Canvas.SetLeft(Balle, balleX);
            Canvas.SetTop(Balle, balleY);
        }

        // --- COLLISIONS & SCORE ---
        private void CheckCollisions()
        {
            if (playerHasBall || enemyHasBall) return;
            Rect b = new Rect(balleX, balleY, Balle.Width, Balle.Height);

            // Rebond Panneaux
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(collisionPanierDroit), Canvas.GetTop(collisionPanierDroit), 10, 100))) balleVelX = -balleVelX * 0.6;
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(collisionPanierGauche), Canvas.GetTop(collisionPanierGauche), 10, 100))) balleVelX = -balleVelX * 0.6;

            // Score Droit (Joueur)
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(zoneScorePanierDroit), Canvas.GetTop(zoneScorePanierDroit), 50, 10)) && balleVelY > 0)
            {
                playerScore += (isSuperShot ? 3 : 2);
                superCharge = Math.Min(100, superCharge + 30);
                SpawnConfetti(Canvas.GetLeft(zoneScorePanierDroit), Canvas.GetTop(zoneScorePanierDroit));
                if (isSuperShot) ShakeScreen(20);
                UpdateScore(); ResetPositions(true);
            }
            // Score Gauche (Ennemi)
            else if (b.IntersectsWith(new Rect(Canvas.GetLeft(zoneScorePanierGauche), Canvas.GetTop(zoneScorePanierGauche), 50, 10)) && balleVelY > 0)
            {
                enemyScore += 2;
                superCharge = Math.Min(100, superCharge + 10);
                SpawnConfetti(Canvas.GetLeft(zoneScorePanierGauche), Canvas.GetTop(zoneScorePanierGauche));
                UpdateScore(); ResetPositions(true);
            }
        }

        private void UpdateScore() { PlayerScoreText.Text = playerScore.ToString(); EnemyScoreText.Text = enemyScore.ToString(); }

        private void MatchClock_Tick(object sender, EventArgs e)
        {
            if (!isGameActive || isPaused) return;
            matchTime--;
            TimerMatch.Text = matchTime.ToString("00");
            if (matchTime <= 0) EndGame();
        }

        private void EndGame()
        {
            isGameActive = false;
            gameTimer.Stop();
            matchClock.Stop();

            GameOverScreen.Visibility = Visibility.Visible;

            // Gestion du meilleur score global
            if (playerScore > UCMenuPrincipal.meilleurScore)
                UCMenuPrincipal.meilleurScore = playerScore;

            // Affichage du score final
            FinalScoreText.Text = $"{playerScore} - {enemyScore}";

            // Affichage Victoire / Défaite / Égalité
            if (playerScore > enemyScore)
            {
                ResultText.Text = "VICTOIRE !";
                ResultText.Foreground = Brushes.Lime; 
            }
            else if (playerScore < enemyScore)
            {
                ResultText.Text = "DÉFAITE...";
                ResultText.Foreground = Brushes.Red; 
            }
            else
            {
                ResultText.Text = "ÉGALITÉ !";
                ResultText.Foreground = Brushes.Orange; 
            }
        }

        // --- EFFETS ---
        private void SpawnConfetti(double x, double y)
        {
            for (int i = 0; i < 30; i++)
            {
                Rectangle rect = new Rectangle { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256))) };
                Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y); canvasJeu.Children.Add(rect);
                particles.Add(new Particle { Shape = rect, VelX = rnd.NextDouble() * 10 - 5, VelY = rnd.NextDouble() * 10 - 5, Life = 60 });
            }
        }

        private void UpdateParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i]; p.Life--;
                Canvas.SetLeft(p.Shape, Canvas.GetLeft(p.Shape) + p.VelX);
                Canvas.SetTop(p.Shape, Canvas.GetTop(p.Shape) + p.VelY);
                p.VelY += 0.5;
                if (p.Life < 20) p.Shape.Opacity = p.Life / 20.0;
                if (p.Life <= 0) { canvasJeu.Children.Remove(p.Shape); particles.RemoveAt(i); }
            }
        }

        private void ShakeScreen(int duration) { shakeDuration = duration; }
        private void UpdateScreenShake()
        {
            if (shakeDuration > 0) { CanvasShake.X = rnd.Next(-10, 11); CanvasShake.Y = rnd.Next(-10, 11); shakeDuration--; }
            else { CanvasShake.X = 0; CanvasShake.Y = 0; }
        }

        // --- SOURIS / TIR ---
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // PROTEGE LE CLICK CONTRE LE CRASH
            try
            {
                this.Focus();
                if (isPaused || !playerHasBall) return;

                isAiming = true;
                if (TrajectoireBalle != null)
                {
                    TrajectoireBalle.Visibility = Visibility.Visible;
                    TrajectoireBalle.Stroke = superCharge >= 100 ? Brushes.Red : Brushes.Yellow;
                }
            }
            catch { }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (isPaused || !isAiming || !playerHasBall) return;
            Point m = e.GetPosition(canvasJeu);
            double vx = (m.X - balleX) * 0.15;
            double vy = (m.Y - balleY) * 0.15;
            // Limites de puissance
            vx = Math.Max(-35, Math.Min(35, vx));
            vy = Math.Max(-35, Math.Min(35, vy));

            DrawTrajectory(balleX, balleY, vx, vy);
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (isPaused || !isAiming || !playerHasBall) return;
                isAiming = false;
                TrajectoireBalle.Visibility = Visibility.Collapsed;
                Point m = e.GetPosition(canvasJeu);

                if (superCharge >= 100) // SUPER TIR
                {
                    isSuperShot = true; superCharge = 0;
                    double tTime = 30.0;
                    balleVelX = (HOOP_RIGHT_X - balleX) / tTime;
                    balleVelY = (HOOP_Y - balleY - 0.5 * (GRAVITY * 0.8) * (tTime * tTime)) / tTime;
                }
                else // TIR NORMAL
                {
                    balleVelX = (m.X - balleX) * 0.15;
                    balleVelY = (m.Y - balleY) * 0.15;
                }
                playerHasBall = false; pickupCooldown = 30;
            }
            catch { }
        }

        private void DrawTrajectory(double sx, double sy, double vx, double vy)
        {
            PointCollection pts = new PointCollection();
            for (int i = 0; i < 15; i++)
            {
                double t = i * 2;
                pts.Add(new Point(sx + vx * t, sy + vy * t + 0.5 * (GRAVITY * 0.8) * t * t));
            }
            TrajectoireBalle.Points = pts;
        }

        private void GameCanvas_Click(object sender, MouseButtonEventArgs e) { this.Focus(); }

        // --- MENUS & BOUTONS ---
        private void PauseButton_Click(object sender, RoutedEventArgs e) { TogglePause(); }
        private void OnKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) TogglePause(); }
        private void OnKeyUp(object sender, KeyEventArgs e) { } // Requis par XAML mais vide

        private void TogglePause()
        {
            if (countdownValue > 0 || !isGameActive) return;
            isPaused = !isPaused;
            if (isPaused) { gameTimer.Stop(); matchClock.Stop(); canvasMenuPause.Visibility = Visibility.Visible; }
            else
            {
                gameTimer.Start(); matchClock.Start();
                canvasMenuPause.Visibility = Visibility.Collapsed;
                this.Focus();
            }
        }

        private void buttonReprendre_Click(object sender, RoutedEventArgs e) { TogglePause(); }
        private void buttonQuitter_Click(object sender, RoutedEventArgs e) { ((MainWindow)Application.Current.MainWindow).Content = new UCMenuPrincipal(); }
        private void ButtonOptions_Click(object sender, RoutedEventArgs e) { MenuOptions.Visibility = Visibility.Visible; }
        private void ButtonRetourOptions_Click(object sender, RoutedEventArgs e) { MenuOptions.Visibility = Visibility.Collapsed; this.Focus(); }

        private void ButtonValider_Click(object sender, RoutedEventArgs e)
        {
            if (DifficulteIA != null) ApplyDifficulty(DifficulteIA.SelectedIndex);
            if (ChoixControles != null) useZQSD = (ChoixControles.SelectedIndex == 1);
            MenuOptions.Visibility = Visibility.Collapsed;
            this.Focus();
        }

        private void buttonRejouer_Click(object sender, RoutedEventArgs e)
        {
            playerScore = 0; enemyScore = 0; superCharge = 0; matchTime = 30; isPaused = false;
            UpdateScore(); SuperBar.Value = 0; TimerMatch.Text = matchTime.ToString("00");
            GameOverScreen.Visibility = Visibility.Collapsed;
            StartCountdown();
        }
    }
}