using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace LanGameClient;

public class MainForm : Form
{
    private NetworkClient? networkClient;
    private readonly System.Windows.Forms.Timer renderTimer;
    private readonly System.Windows.Forms.Timer inputTimer;
    private readonly HashSet<Keys> pressedKeys = new();
    private bool isFullscreen = false;
    private FormWindowState previousWindowState;
    private FormBorderStyle previousBorderStyle;
    private Rectangle previousBounds;
    private string gamePhase = "CONNECTING";
    private string previousPhase = "CONNECTING";
    private int localPlayerId = -1;
    private readonly List<PlayerInfo> players = new();
    private readonly List<CoinInfo> coins = new();
    private readonly List<SnakeSegmentInfo> snakeSegments = new();
    private readonly List<WallInfo> walls = new();
    private readonly object lockObj = new();

    public MainForm()
    {
        Text = "LAN Game";
        ClientSize = new Size(1280, 720);
        DoubleBuffered = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;

        var handle = Handle;

        renderTimer = new System.Windows.Forms.Timer { Interval = 16 };
        renderTimer.Tick += (s, e) => Invalidate();

        inputTimer = new System.Windows.Forms.Timer { Interval = 50 };
        inputTimer.Tick += SendInput;

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        Paint += OnPaint;
        FormClosing += (s, e) => networkClient?.Disconnect();

        ShowConnectionDialog();
    }

    private void ShowConnectionDialog()
    {
        using var dialog = new Form
        {
            Text = "Connect to Server",
            ClientSize = new Size(350, 200),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var lblNickname = new Label
        {
            Text = "Nickname:",
            Location = new Point(20, 20),
            AutoSize = true,
        };
        var txtNickname = new TextBox
        {
            Location = new Point(120, 20),
            Width = 200,
            Text = "Player",
        };

        var lblIp = new Label
        {
            Text = "Server IP:",
            Location = new Point(20, 60),
            AutoSize = true,
        };
        var txtIp = new TextBox
        {
            Location = new Point(120, 60),
            Width = 200,
            Text = "127.0.0.1",
        };

        var lblPort = new Label
        {
            Text = "Port:",
            Location = new Point(20, 100),
            AutoSize = true,
        };
        var txtPort = new TextBox
        {
            Location = new Point(120, 100),
            Width = 200,
            Text = "5000",
        };

        var btnConnect = new Button
        {
            Text = "Connect",
            Location = new Point(120, 140),
            Width = 100,
            DialogResult = DialogResult.OK,
        };

        dialog.Controls.AddRange(
            new Control[] { lblNickname, txtNickname, lblIp, txtIp, lblPort, txtPort, btnConnect }
        );
        dialog.AcceptButton = btnConnect;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var nickname = txtNickname.Text;
            var ip = txtIp.Text;
            var port = int.Parse(txtPort.Text);

            networkClient = new NetworkClient(ip, port, nickname, this);
            networkClient.Connect();
            renderTimer.Start();
            inputTimer.Start();
        }
        else
        {
            Close();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11 || keyData == (Keys.Alt | Keys.Enter))
        {
            ToggleFullscreen();
            return true;
        }

        if (keyData == Keys.Escape && isFullscreen)
        {
            ToggleFullscreen();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        pressedKeys.Add(e.KeyCode);

        if (e.KeyCode == Keys.N && gamePhase == "GAME_OVER")
        {
            networkClient?.SendRestart();
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        pressedKeys.Remove(e.KeyCode);
    }

    private void ToggleFullscreen()
    {
        if (isFullscreen)
        {
            FormBorderStyle = previousBorderStyle;
            Bounds = previousBounds;
            WindowState = previousWindowState;
            isFullscreen = false;
        }
        else
        {
            previousWindowState = WindowState;
            previousBorderStyle = FormBorderStyle;
            previousBounds = Bounds;

            var screen = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault();
            if (screen != null)
            {
                FormBorderStyle = FormBorderStyle.None;
                Bounds = screen.Bounds;
                WindowState = FormWindowState.Normal;
                isFullscreen = true;
            }
        }
    }

    private void SendInput(object? sender, EventArgs e)
    {
        if (networkClient == null || gamePhase != "PLAYING")
            return;

        var input = "";
        if (pressedKeys.Contains(Keys.W) || pressedKeys.Contains(Keys.Up))
            input += "U";
        if (pressedKeys.Contains(Keys.S) || pressedKeys.Contains(Keys.Down))
            input += "D";
        if (pressedKeys.Contains(Keys.A) || pressedKeys.Contains(Keys.Left))
            input += "L";
        if (pressedKeys.Contains(Keys.D) || pressedKeys.Contains(Keys.Right))
            input += "R";

        networkClient.SendInput(input);
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(20, 20, 30));

        if (gamePhase == "CONNECTING")
        {
            DrawCenteredText(g, "Connecting to server...", Brushes.White, ClientSize.Height / 2);
            return;
        }

        DrawArena(g);

        lock (lockObj)
        {
            foreach (var wall in walls)
            {
                g.FillRectangle(Brushes.Gray, wall.X + 20, wall.Y + 30, wall.Width, wall.Height);
                g.DrawRectangle(Pens.White, wall.X + 20, wall.Y + 30, wall.Width, wall.Height);
            }

            if (snakeSegments.Count > 0)
            {
                var head = snakeSegments[0];
                g.FillEllipse(Brushes.OrangeRed, head.X + 20, head.Y + 30, 22, 22);
                g.DrawEllipse(new Pen(Color.DarkRed, 3), head.X + 20, head.Y + 30, 22, 22);

                for (int i = 1; i < snakeSegments.Count; i++)
                {
                    var segment = snakeSegments[i];
                    g.FillEllipse(Brushes.LimeGreen, segment.X + 20, segment.Y + 30, 20, 20);
                    g.DrawEllipse(new Pen(Color.DarkGreen, 2), segment.X + 20, segment.Y + 30, 20, 20);
                }
            }

            foreach (var coin in coins)
            {
                g.FillEllipse(Brushes.Gold, coin.X + 20, coin.Y + 30, 20, 20);
            }

            bool blinkOn = (Environment.TickCount / 250) % 2 == 0;

            foreach (var player in players)
            {
                bool stuck = false;
                foreach (var wall in walls)
                {
                    if (player.X < wall.X + wall.Width && player.X + 30 > wall.X &&
                        player.Y < wall.Y + wall.Height && player.Y + 30 > wall.Y)
                    {
                        stuck = true;
                        break;
                    }
                }

                Brush brush = GetPlayerBrush(player.Color);
                Pen borderPen = Pens.Black;

                if (stuck && blinkOn)
                {
                    brush = Brushes.Magenta;
                    borderPen = Pens.DeepPink;
                }

                g.FillRectangle(brush, player.X + 20, player.Y + 30, 30, 30);
                g.DrawRectangle(borderPen, player.X + 20, player.Y + 30, 30, 30);

                var nameSize = g.MeasureString(player.Name, Font);
                g.DrawString(player.Name, Font, Brushes.White,
                    player.X + 20 + 15 - nameSize.Width / 2, player.Y + 10);
            }
        }

        DrawHud(g);

        if (gamePhase == "GAME_OVER")
        {
            DrawGameOver(g);
        }
    }

    private void DrawArena(Graphics g)
    {
        var arenaRect = new Rectangle(20, 30, 1240, 660);
        g.DrawRectangle(new Pen(Color.Gray, 3), arenaRect);
    }

    private void DrawHud(Graphics g)
    {
        lock (lockObj)
        {
            var hudText = string.Join(
                "  |  ",
                players.OrderBy(p => p.Id).Select(p => $"{p.Name}: {p.Score}")
            );
            hudText += $"  |  Snake: {snakeSegments.Count}  Walls: {walls.Count}";
            g.DrawString(
                hudText,
                new Font(Font.FontFamily, 12, FontStyle.Bold),
                Brushes.White,
                20,
                5
            );
        }
    }

    private void DrawGameOver(Graphics g)
    {
        var overlay = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
        g.FillRectangle(overlay, ClientRectangle);

        lock (lockObj)
        {
            var winner = players.OrderByDescending(p => p.Score).FirstOrDefault();
            if (winner != null)
            {
                var winText = $"{winner.Name} Wins!";
                var font = new Font(Font.FontFamily, 48, FontStyle.Bold);
                DrawCenteredText(g, winText, Brushes.Yellow, ClientSize.Height / 2 - 100, font);

                var ranking = string.Join(
                    "\n",
                    players
                        .OrderByDescending(p => p.Score)
                        .Select((p, i) => $"{i + 1}. {p.Name} - {p.Score} points")
                );
                DrawCenteredText(
                    g,
                    ranking,
                    Brushes.White,
                    ClientSize.Height / 2,
                    new Font(Font.FontFamily, 16)
                );

                DrawCenteredText(
                    g,
                    "Press N for new game",
                    Brushes.LightGray,
                    ClientSize.Height / 2 + 150
                );
            }
        }
    }

    private void DrawCenteredText(Graphics g, string text, Brush brush, int y, Font? font = null)
    {
        font ??= Font;
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, (ClientSize.Width - size.Width) / 2, y);
    }

    private Brush GetPlayerBrush(string color) =>
        color switch
        {
            "Red" => Brushes.Red,
            "Blue" => Brushes.DodgerBlue,
            "Green" => Brushes.Lime,
            "Yellow" => Brushes.Yellow,
            _ => Brushes.White,
        };

    private void PlayCoinSound()
    {
        SystemSounds.Asterisk.Play();
    }

    private void PlayHitSound()
    {
        SystemSounds.Hand.Play();
    }

    private void PlayGameOverSound()
    {
        SystemSounds.Exclamation.Play();
    }

    public void SetLocalPlayerId(int id)
    {
        localPlayerId = id;
    }

    public void UpdateGameState(
        string phase,
        List<PlayerInfo> newPlayers,
        List<CoinInfo> newCoins,
        List<SnakeSegmentInfo> newSnake,
        List<WallInfo> newWalls
    )
    {
        lock (lockObj)
        {
            var oldPhase = gamePhase;
            PlayerInfo? oldLocal = null;
            PlayerInfo? newLocal = null;

            if (localPlayerId >= 0)
            {
                oldLocal = players.FirstOrDefault(p => p.Id == localPlayerId);
                newLocal = newPlayers.FirstOrDefault(p => p.Id == localPlayerId);
            }

            if (oldPhase == "PLAYING" && phase == "GAME_OVER")
            {
                PlayGameOverSound();
            }

            if (oldLocal != null && newLocal != null)
            {
                if (newLocal.Score > oldLocal.Score)
                {
                    PlayCoinSound();
                }

                bool positionReset =
                    Math.Abs(newLocal.X - oldLocal.X) > 60
                    || Math.Abs(newLocal.Y - oldLocal.Y) > 60;

                if (oldLocal.Score > 0 && newLocal.Score == 0 && positionReset)
                {
                    PlayHitSound();
                }
            }

            previousPhase = oldPhase;
            gamePhase = phase;
            players.Clear();
            players.AddRange(newPlayers);
            coins.Clear();
            coins.AddRange(newCoins);
            snakeSegments.Clear();
            snakeSegments.AddRange(newSnake);
            walls.Clear();
            walls.AddRange(newWalls);
        }
    }
}

public class PlayerInfo
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Score { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
}

public class CoinInfo
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class SnakeSegmentInfo
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class WallInfo
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
