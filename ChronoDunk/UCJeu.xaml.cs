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

        // --- PARAMETRES ---
        int currentGameMode;
        bool useZQSD = false;

        // --- CONSTANTES ---
        const double WINDOW_WIDTH = 1280;
        const double WINDOW_HEIGHT = 720;
        const double GROUND_Y = 650;
        const double GRAVITY = 1.5;

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

        // --- PARAMETRES INTELLIGENCE ARTIFICIELLE ---
        int aiReactionSpeed = 40;
        double aiErrorMargin = 1.5;
        int aiStealAggro = 3;
        int aiShootTimer = 0;
        int aiActionTimer = 0;

        // NOUVELLES VARIABLES IA
        int aiJumpCooldown = 0;        // Pour éviter que l'IA saute en boucle
        double aiShootingRange = 300;  // Distance de tir préférée

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

            // 1. Stats du joueur
            if (p1Stats != null)
            {
                baseSpeed = p1Stats.Speed;
                jumpForce = p1Stats.JumpForce;
                try { Player.Source = new BitmapImage(new Uri(p1Stats.ImagePath, UriKind.Relative)); } catch { }
            }

            // 2. Difficulté par défaut
            int defaultLevel = 1;
            if (DifficulteIA != null) DifficulteIA.SelectedIndex = defaultLevel;
            ApplyDifficulty(defaultLevel);

            // 3. Ajustement Joueur vs Joueur
            if (currentGameMode == 2) enemySpeed = baseSpeed;

            this.Loaded += (s, e) =>
            {
                this.Focus();
                Canvas.SetLeft(CountdownText, (WINDOW_WIDTH / 2) - 50);
                StartCountdown();
            };

            gameTimer.Interval = TimeSpan.FromMilliseconds(16); gameTimer.Tick += GameLoop; gameTimer.Start();
            matchClock.Interval = TimeSpan.FromSeconds(1); matchClock.Tick += MatchClock_Tick;
            countdownTimer.Interval = TimeSpan.FromSeconds(1); countdownTimer.Tick += Countdown_Tick;
        }

        // =========================================================
        // GESTION DIFFICULTÉ (AMÉLIORÉE)
        // =========================================================

        private void ComboDifficulty_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void ApplyDifficulty(int level)
        {
            if (currentGameMode == 2) return;

            switch (level)
            {
                case 0: // FACILE
                    enemySpeed = 7.0;
                    aiReactionSpeed = 60; // Lent
                    aiErrorMargin = 4.0; // Tire mal
                    aiStealAggro = 150; // Vole peu
                    aiShootingRange = 200; // Tire de près uniquement
                    break;
                case 1: // NORMAL
                    enemySpeed = 10.0;
                    aiReactionSpeed = 30;
                    aiErrorMargin = 1.5;
                    aiStealAggro = 60;
                    aiShootingRange = 400; // Tire à mi-distance
                    break;
                case 2: // NBA STAR
                    enemySpeed = 15;
                    aiReactionSpeed = 5; // Réflexes éclairs
                    aiErrorMargin = 0; // Sniper
                    aiStealAggro = 20; // Très agressif
                    aiShootingRange = 650; // Tire à 3 points
                    break;
            }
        }

        // =========================================================
        // IA (AMÉLIORÉE)
        // =========================================================
        // =========================================================
        // IA (CORRIGÉE ET NETTOYÉE)
        // =========================================================
        private void UpdateAI()
        {
            // 1. DÉCLARATION DES VARIABLES (Au tout début pour être accessibles partout)
            double enemyX = Canvas.GetLeft(Enemy);
            double enemyY = Canvas.GetTop(Enemy);
            double playerX = Canvas.GetLeft(Player); // <--- C'est ici qu'elle est définie !

            // Coordonnée X du panier gauche (Cible)
            double hoopX = 238;

            aiActionTimer++;
            if (aiJumpCooldown > 0) aiJumpCooldown--;

            // --- CAS 1 : L'IA A LA BALLE (ATTAQUE) ---
            if (enemyHasBall)
            {
                double distanceToHoop = enemyX - hoopX;

                // Si on est trop près (< 150px), on recule pour prendre de l'élan
                if (distanceToHoop < 150)
                {
                    MoveChar(Enemy, enemySpeed); // Recule vers la droite
                    SetFacing(Enemy, false);     // Regarde le panier
                }
                else if (distanceToHoop <= aiShootingRange)
                {
                    aiShootTimer++;
                    if (aiShootTimer > aiReactionSpeed)
                    {
                        EnemySmartShoot();
                        aiShootTimer = 0;
                    }
                }
                else
                {
                    MoveChar(Enemy, -enemySpeed);
                    SetFacing(Enemy, false);
                }
            }
            // --- CAS 2 : BALLE LIBRE (REBOND) ---
            else if (!playerHasBall)
            {
                if (balleX > enemyX + 10) { MoveChar(Enemy, enemySpeed); SetFacing(Enemy, true); }
                else if (balleX < enemyX - 10) { MoveChar(Enemy, -enemySpeed); SetFacing(Enemy, false); }

                if (balleY < enemyY - 50 && Math.Abs(balleX - enemyX) < 80 && !enemyJumping && aiJumpCooldown == 0)
                {
                    enemyVelY = -28;
                    enemyJumping = true;
                    aiJumpCooldown = 60;
                }
            }
            // --- CAS 3 : DÉFENSE (C'est ici que playerX est utilisé) ---
            else
            {
                aiShootTimer = 0;

                // --- LOGIQUE D'AGRESSIVITÉ ---
                double defensiveSpot;

                // Si le Timer d'action dépasse l'aggro, l'IA cible le JOUEUR (Attaque)
                if (aiActionTimer > aiStealAggro)
                {
                    defensiveSpot = playerX; // Elle te fonce dessus !
                }
                else
                {
                    // Sinon, elle garde une distance de sécurité (Garde)
                    defensiveSpot = playerX + 150;
                }

                // MOUVEMENT
                if (defensiveSpot > enemyX + 20)
                {
                    MoveChar(Enemy, enemySpeed);
                    SetFacing(Enemy, true);
                }
                else if (defensiveSpot < enemyX - 20)
                {
                    MoveChar(Enemy, -enemySpeed);
                    SetFacing(Enemy, false);
                }

                // --- TENTATIVE DE VOL (STEAL) ---
                // Si l'IA est proche (< 80px) ET en mode attaque
                if (Math.Abs(enemyX - playerX) < 80 && aiActionTimer > aiStealAggro)
                {
                    AttemptSteal(false);
                    aiActionTimer = 0; // Reset du timer après tentative
                }

                // --- SAUT DÉFENSIF (CONTRE) ---
                if (playerJumping && !enemyJumping && Math.Abs(enemyX - playerX) < 100 && aiJumpCooldown == 0)
                {
                    if (rnd.Next(0, 10) > 1) // 80% de chance de sauter pour contrer
                    {
                        enemyVelY = -28;
                        enemyJumping = true;
                        aiJumpCooldown = 40;
                    }
                }
            }
        }

        private void EnemySmartShoot()
        {
            enemyHasBall = false;

            // CORRECTION MAJEURE : La cible est le panier gauche (environ 238px) et non 40px
            double targetX = 238;
            double currentX = Canvas.GetLeft(Enemy);

            // Distance réelle à parcourir
            double distance = currentX - targetX;

            // --- ADAPTATION DE LA PHYSIQUE ---
            // Si on est loin (> 400px), on fait un tir haut (cloche). 
            // Si on est près, on fait un tir plus tendu.

            double flightTime; // Temps de vol estimé (en frames)

            if (distance > 450)
            {
                balleVelY = -32; // Tir haut (3 points)
                flightTime = 43.0;
            }
            else
            {
                balleVelY = -24; // Tir mi-distance (plus précis)
                flightTime = 32.0;
            }

            // Formule : Vitesse X = Distance / Temps
            double perfectVelX = -(distance / flightTime);

            // Ajout de l'imprécision humaine
            double randomError = (rnd.NextDouble() * (aiErrorMargin * 2)) - aiErrorMargin;
            balleVelX = perfectVelX + randomError;

            // L'IA saute pour tirer
            if (!enemyJumping)
            {
                enemyVelY = -15;
                enemyJumping = true;
            }

            pickupCooldown = 20;
        }

        // =========================================================
        // LOGIQUE JEU
        // =========================================================

        private void StartCountdown()
        {
            isGameActive = false; matchClock.Stop(); ResetPositions(false);
            countdownValue = 3; CountdownText.Text = "3"; CountdownText.Visibility = Visibility.Visible; CountdownText.Foreground = Brushes.Yellow;
            countdownTimer.Start();
        }

        private void Countdown_Tick(object sender, EventArgs e)
        {
            countdownValue--;
            if (countdownValue > 0) { CountdownText.Text = countdownValue.ToString(); CountdownText.FontSize = 100; }
            else if (countdownValue == 0) { CountdownText.Text = "GO!"; CountdownText.Foreground = Brushes.Lime; }
            else { countdownTimer.Stop(); CountdownText.Visibility = Visibility.Collapsed; isGameActive = true; matchClock.Start(); }
        }

        private void ResetPositions(bool withDelay = true)
        {
            balleX = (WINDOW_WIDTH / 2) - (Balle.Width / 2); balleY = 100;
            balleVelX = 0; balleVelY = 0; ballAngle = 0; isSuperShot = false;
            playerHasBall = false; enemyHasBall = false;
            pickupCooldown = withDelay ? 60 : 0;
            aiShootTimer = 0; aiActionTimer = 0; isAiming = false;
            TrajectoireBalle.Visibility = Visibility.Collapsed;
            Canvas.SetLeft(Player, 200); SetFacing(Player, true);
            Canvas.SetLeft(Enemy, WINDOW_WIDTH - 200 - Enemy.Width); SetFacing(Enemy, false);
            UpdateBallVisuals();
        }

        private void TogglePause()
        {
            if (countdownValue > 0 || !isGameActive) return;
            if (isPaused)
            {
                isPaused = false;
                canvasMenuPause.Visibility = Visibility.Collapsed;
                gameTimer.Start(); matchClock.Start(); this.Focus();
            }
            else
            {
                isAiming = false; TrajectoireBalle.Visibility = Visibility.Collapsed;
                isPaused = true;
                canvasMenuPause.Visibility = Visibility.Visible;
                gameTimer.Stop(); matchClock.Stop();
            }
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
            HandleBallLogic(); CheckCollisions();
            SuperBar.Value = superCharge;
        }

        private void SpawnConfetti(double x, double y)
        {
            for (int i = 0; i < 30; i++)
            {
                Rectangle rect = new Rectangle { Width = 8, Height = 8 };
                byte r = (byte)rnd.Next(256); byte g = (byte)rnd.Next(256); byte b = (byte)rnd.Next(256);
                rect.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
                Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y); canvasJeu.Children.Add(rect);
                particles.Add(new Particle { Shape = rect, VelX = rnd.NextDouble() * 10 - 5, VelY = rnd.NextDouble() * 10 - 5, Life = 60 });
            }
        }

        private void UpdateParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i]; p.Life--;
                Canvas.SetLeft(p.Shape, Canvas.GetLeft(p.Shape) + p.VelX); Canvas.SetTop(p.Shape, Canvas.GetTop(p.Shape) + p.VelY); p.VelY += 0.5;
                if (p.Life < 20) p.Shape.Opacity = p.Life / 20.0;
                if (p.Life <= 0) { canvasJeu.Children.Remove(p.Shape); particles.RemoveAt(i); }
            }
        }

        private void ShakeScreen(int duration) { shakeDuration = duration; }
        private void UpdateScreenShake() { if (shakeDuration > 0) { CanvasShake.X = rnd.Next(-10, 11); CanvasShake.Y = rnd.Next(-10, 11); shakeDuration--; } else { CanvasShake.X = 0; CanvasShake.Y = 0; } }

        // --- GESTION DES TOUCHES ---
        private void HandlePlayer1Input()
        {
            double currentSpeed = Keyboard.IsKeyDown(Key.LeftShift) ? (baseSpeed + sprintBonus) : baseSpeed;

            Key keyLeft = useZQSD ? Key.Q : Key.Left;
            Key keyRight = useZQSD ? Key.D : Key.Right;
            Key keyUp = useZQSD ? Key.Z : Key.Up;
            Key keySteal = useZQSD ? Key.S : Key.Down;

            if (Keyboard.IsKeyDown(keyLeft)) { MoveChar(Player, -currentSpeed); SetFacing(Player, false); }
            if (Keyboard.IsKeyDown(keyRight)) { MoveChar(Player, currentSpeed); SetFacing(Player, true); }
            if (Keyboard.IsKeyDown(keyUp) && !playerJumping) { playerVelY = jumpForce; playerJumping = true; }
            if (Keyboard.IsKeyDown(keySteal)) AttemptSteal(true);
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
            if (newLeft > -50 && newLeft < WINDOW_WIDTH - 150) Canvas.SetLeft(person, newLeft);
        }

        private void AttemptSteal(bool isP1)
        {
            if (pickupCooldown > 0) return;
            if (isP1 && enemyHasBall && Math.Abs(Canvas.GetLeft(Player) - Canvas.GetLeft(Enemy)) < 100)
            { enemyHasBall = false; playerHasBall = true; pickupCooldown = 15; isAiming = false; TrajectoireBalle.Visibility = Visibility.Collapsed; }
            else if (!isP1 && playerHasBall && Math.Abs(Canvas.GetLeft(Player) - Canvas.GetLeft(Enemy)) < 100)
            { playerHasBall = false; enemyHasBall = true; pickupCooldown = 15; isAiming = false; TrajectoireBalle.Visibility = Visibility.Collapsed; }
        }

        private void ApplyPhysics(UIElement charObj, ref double velY, ref bool isJumping)
        {
            double top = Canvas.GetTop(charObj) + velY; velY += GRAVITY;
            if (top + ((FrameworkElement)charObj).Height >= GROUND_Y) { top = GROUND_Y - ((FrameworkElement)charObj).Height; isJumping = false; velY = 0; }
            Canvas.SetTop(charObj, top);
        }

        private void HandleBallLogic()
        {
            if (playerHasBall) { balleX = Canvas.GetLeft(Player) + (Player.Width / 2) - (Balle.Width / 2); balleY = Canvas.GetTop(Player) + (Player.Height / 3); balleVelX = 0; balleVelY = 0; isSuperShot = false; }
            else if (enemyHasBall) { balleX = Canvas.GetLeft(Enemy) + (Enemy.Width / 2) - (Balle.Width / 2); balleY = Canvas.GetTop(Enemy) + (Enemy.Height / 3); balleVelX = 0; balleVelY = 0; }
            else
            {
                balleX += balleVelX;
                balleY += balleVelY; // Vitesse ajoutée à la position
                balleVelY += GRAVITY * 0.8;
                ballAngle += balleVelX * 2;

                RotateTransform rotate = Balle.RenderTransform as RotateTransform;
                if (rotate == null) { rotate = new RotateTransform(); Balle.RenderTransform = rotate; }
                rotate.Angle = ballAngle;

                if (balleY + Balle.Height >= GROUND_Y) { balleY = GROUND_Y - Balle.Height; balleVelY = -balleVelY * 0.6; balleVelX *= 0.95; }
                if (balleX > WINDOW_WIDTH + 100 || balleX < -150) ResetPositions(true);

                if (pickupCooldown == 0)
                {
                    Rect ballRect = new Rect(balleX, balleY, Balle.Width, Balle.Height);
                    if (new Rect(Canvas.GetLeft(Player) - 10, Canvas.GetTop(Player), Player.Width + 20, Player.Height).IntersectsWith(ballRect)) { playerHasBall = true; isAiming = false; }
                    if (new Rect(Canvas.GetLeft(Enemy) - 10, Canvas.GetTop(Enemy), Enemy.Width + 20, Enemy.Height).IntersectsWith(ballRect)) { enemyHasBall = true; }
                }
            }
            UpdateBallVisuals();
        }

        private void UpdateBallVisuals() { Balle.Opacity = (isSuperShot && DateTime.Now.Millisecond % 100 < 50) ? 0.5 : 1.0; Canvas.SetLeft(Balle, balleX); Canvas.SetTop(Balle, balleY); }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isPaused) return;
            if (playerHasBall)
            {
                isAiming = true;
                TrajectoireBalle.Visibility = Visibility.Visible;
                TrajectoireBalle.Stroke = superCharge >= 100 ? Brushes.Red : Brushes.Yellow;
            }
            this.Focus();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (isPaused) return;
            if (isAiming && playerHasBall)
            {
                Point mousePos = e.GetPosition(canvasJeu);
                double velX = (mousePos.X - balleX) * 0.15;
                double velY = (mousePos.Y - balleY) * 0.15;
                if (velX > 35) velX = 35; if (velX < -35) velX = -35;
                if (velY > 35) velY = 35; if (velY < -35) velY = -35;
                DrawTrajectory(balleX, balleY, velX, velY);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isPaused) return;
            if (isAiming && playerHasBall)
            {
                isAiming = false;
                TrajectoireBalle.Visibility = Visibility.Collapsed;
                Point mousePos = e.GetPosition(canvasJeu);

                // --- GESTION DU SUPER TIR ---
                if (superCharge >= 100)
                {
                    isSuperShot = true;
                    superCharge = 0;

                    // 1. Cible : Le panier Droit (Coordonnées approximatives)
                    double targetX = 1195; // Position X du panier droit (ajusté pour 1280px)
                    double targetY = 180;  // Hauteur idéale pour un Swish

                    // 2. Temps de vol (plus c'est petit, plus la balle va vite)
                    double flightTime = 30.0; // Très rapide (Fireball !)

                    // 3. Calcul de la vitesse X (Distance / Temps)
                    balleVelX = (targetX - balleX) / flightTime;

                    // 4. Calcul de la vitesse Y (Physique inversée pour tomber pile dans le panier)
                    // Formule : Vy = (Y_cible - Y_depart - 0.5 * Gravité * t^2) / t
                    double effectiveGravity = GRAVITY * 0.8; // Ta gravité réelle dans HandleBallLogic
                    balleVelY = (targetY - balleY - 0.5 * effectiveGravity * (flightTime * flightTime)) / flightTime;
                }
                // --- TIR NORMAL ---
                else
                {
                    balleVelX = (mousePos.X - balleX) * 0.15;
                    balleVelY = (mousePos.Y - balleY) * 0.15;
                    playerHasBall = false;
                    pickupCooldown = 30;
                }

                playerHasBall = false;
                pickupCooldown = 30;
            }
        }

        private void DrawTrajectory(double startX, double startY, double velX, double velY)
        {
            PointCollection points = new PointCollection();
            for (int i = 0; i < 15; i++) { double t = i * 2; points.Add(new Point(startX + velX * t, startY + velY * t + 0.5 * (GRAVITY * 0.8) * t * t)); }
            TrajectoireBalle.Points = points;
        }

        private void CheckCollisions()
        {
            if (playerHasBall || enemyHasBall) return;
            Rect b = new Rect(balleX, balleY, Balle.Width, Balle.Height);
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(collisionPanierDroit), Canvas.GetTop(collisionPanierDroit), 10, 100))) balleVelX = -balleVelX * 0.6;
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(zoneScorePanierDroit), Canvas.GetTop(zoneScorePanierDroit), 50, 10)) && balleVelY > 0)
            {
                playerScore += (isSuperShot ? 3 : 2); superCharge += 30; if (superCharge > 100) superCharge = 100;
                SpawnConfetti(Canvas.GetLeft(zoneScorePanierDroit), Canvas.GetTop(zoneScorePanierDroit)); if (isSuperShot) ShakeScreen(20); UpdateScore(); ResetPositions(true);
            }
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(collisionPanierGauche), Canvas.GetTop(collisionPanierGauche), 10, 100))) balleVelX = -balleVelX * 0.6;
            if (b.IntersectsWith(new Rect(Canvas.GetLeft(zoneScorePanierGauche), Canvas.GetTop(zoneScorePanierGauche), 50, 10)) && balleVelY > 0)
            {
                enemyScore += 2; superCharge += 10; if (superCharge > 100) superCharge = 100;
                SpawnConfetti(Canvas.GetLeft(zoneScorePanierGauche), Canvas.GetTop(zoneScorePanierGauche)); UpdateScore(); ResetPositions(true);
            }
        }

        private void UpdateScore() { PlayerScoreText.Text = playerScore.ToString(); EnemyScoreText.Text = enemyScore.ToString(); }
        private void MatchClock_Tick(object sender, EventArgs e) { if (!isGameActive || isPaused) return; matchTime--; TimerMatch.Text = matchTime.ToString("00"); if (matchTime <= 0) EndGame(); }
        private void EndGame()
        {
            isGameActive = false; gameTimer.Stop(); matchClock.Stop(); GameOverScreen.Visibility = Visibility.Visible;
            if (playerScore > UCMenuPrincipal.meilleurScore) UCMenuPrincipal.meilleurScore = playerScore;
            FinalScoreText.Text = $"{playerScore} - {enemyScore}";
        }

        private void QuitGame_Click(object sender, RoutedEventArgs e)
        {
            playerScore = 0; enemyScore = 0; superCharge = 0; matchTime = 60; isPaused = false;
            canvasMenuPause.Visibility = Visibility.Collapsed; GameOverScreen.Visibility = Visibility.Collapsed;
            UpdateScore(); StartCountdown(); gameTimer.Start();
        }

        private void QuitToMenu_Click(object sender, RoutedEventArgs e)
        {
            gameTimer.Stop(); matchClock.Stop();
            UCMenuPrincipal menu = new UCMenuPrincipal();
            ((MainWindow)Application.Current.MainWindow).Content = menu;
        }

        private void GameCanvas_Click(object sender, MouseButtonEventArgs e) { this.Focus(); }
        private void buttonQuitter_Click(object sender, RoutedEventArgs e) { this.Content = new UCMenuPrincipal(); }

        private void buttonReprendre_Click(object sender, RoutedEventArgs e)
        {
            gameTimer.Start();
            matchClock.Start();
            isPaused = false;
            countdownTimer.Start();
            canvasMenuPause.Visibility = Visibility.Collapsed;
            this.Focus();
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e) { gameTimer.Stop(); matchClock.Stop(); countdownTimer.Stop(); canvasMenuPause.Visibility = Visibility.Visible; }
        private void SetFacing(Image t, bool r) { ((ScaleTransform)t.RenderTransform).ScaleX = r ? 1 : -1; }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) TogglePause();
            if (isPaused) return;
            Key keyUp = useZQSD ? Key.Z : Key.Up;
            if (e.Key == keyUp && !playerJumping) { playerVelY = jumpForce; playerJumping = true; }
        }
        private void OnKeyUp(object sender, KeyEventArgs e) { }

        // GESTION MENUS
        private void ButtonRetourOptions_Click(object sender, RoutedEventArgs e) { canvasMenuPause.Visibility = Visibility.Visible; MenuOptions.Visibility = Visibility.Collapsed; this.Focus(); }
        private void ButtonOptions_Click(Object sender, RoutedEventArgs e) { MenuOptions.Visibility = Visibility.Visible; }
        private void ButtonValider_Click(object sender, RoutedEventArgs e)
        {
            if (DifficulteIA != null) { int level = DifficulteIA.SelectedIndex; ApplyDifficulty(level); }
            if (ChoixControles != null) useZQSD = (ChoixControles.SelectedIndex == 1);

            MenuOptions.Visibility = Visibility.Collapsed;
            if (isPaused) canvasMenuPause.Visibility = Visibility.Visible;
            this.Focus();
        }

        private void buttonRejouer_Click(object sender, RoutedEventArgs e)
        {
            playerScore = 0; enemyScore = 0; superCharge = 0; matchTime = 30; isPaused = false;
            UpdateScore(); SuperBar.Value = 0; TimerMatch.Text = matchTime.ToString("00");
            GameOverScreen.Visibility = Visibility.Collapsed; canvasMenuPause.Visibility = Visibility.Collapsed;
            gameTimer.Start(); StartCountdown(); this.Focus();
        }
    }
}