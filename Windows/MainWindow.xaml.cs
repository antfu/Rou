﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using MaterialIcons;
using Rou.Utils;
using Rou.Actions;
using Rou.Windows;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace Rou
{
    public partial class MainWindow : Window
    {
        private ActionsCollection actions;

        private bool _shown = false;
        private bool _disabled = false;
        private KeyboardHookEx hookEx = new KeyboardHookEx();
        private NotifyIcon notifyIcon = null;
        private Action currentAction = null;
        private Storyboard StoryboardIn;
        private Storyboard StoryboardOut;
        private readonly IntPtr hWnd;
        private Configuration config;
        private bool hoveredOnSubfuncLabel;

        private System.Windows.Forms.MenuItem menuEnable;
        private System.Windows.Forms.MenuItem menuExit;
        private System.Windows.Forms.MenuItem menuVersion;
        private System.Windows.Forms.MenuItem menuOption;


        public Dictionary<string, List<UIElement>> sectorCache;
        public List<UIElement> defaultSectorCache;

        public bool ShowText { get; private set; } = false;

        public MainWindow()
        {
            IsHitTestVisible = false;
            InitializeComponent();
            init();
            hWnd = new WindowInteropHelper(this).EnsureHandle();
            API.SetWindowTrans(hWnd);
        }

        public void init()
        {

#if DEBUG
            debugLabel.Visibility = Visibility.Visible;
#else
            debugLabel.Visibility = Visibility.Collapsed;
#endif

            var presetReleaser = new PresetReleaser();
            presetReleaser.ReleaseAll();

            config = new Configuration(@"configs");
            config.Load();
            actions = config.DefaultActions;
            sectorCache = new Dictionary<string, List<UIElement>>();

            hookEx.HookedKeys.Add(config.Hotkey);
            hookEx.KeyDown += HookEx_KeyDown;
            hookEx.KeyUp += HookEx_KeyUp;

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = Properties.Resources.icon_32_ico;
            notifyIcon.BalloonTipTitle = "Rou";
            notifyIcon.BalloonTipText = "";
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu();
            string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            menuVersion = new System.Windows.Forms.MenuItem { Text = "v" + assemblyVersion };
            menuVersion.Enabled = false;
            menuEnable = new System.Windows.Forms.MenuItem { Text = "Enable" };
            menuEnable.Checked = true;
            menuOption = new System.Windows.Forms.MenuItem { Text = "Options" };
            menuExit = new System.Windows.Forms.MenuItem { Text = "Exit" };
            // TODO: Temporary disabled option menu 
            notifyIcon.ContextMenu.MenuItems.Add(menuOption);
            notifyIcon.ContextMenu.MenuItems.Add(menuVersion);
            notifyIcon.ContextMenu.MenuItems.Add(menuEnable);
            notifyIcon.ContextMenu.MenuItems.Add(menuExit);
            menuOption.Click += (s, e) =>
            {
                (new IconSelectWindow()).Show();
            };
            menuExit.Click += (s, e) =>
            {
                Exit();
            };
            menuEnable.Click += (s, e) =>
            {
                if (menuEnable.Checked)
                {
                    menuEnable.Checked = false;
                    Disable();
                }
                else
                {
                    menuEnable.Checked = true;
                    Enable();
                }
            };

            rouBack.Width = C.RouRaduis * 2;
            rouBack.Height = C.RouRaduis * 2;
            cavans.Width = C.RouRaduis * 2;
            cavans.Height = C.RouRaduis * 2;
            Width = C.RouRaduis * 2 + C.RouPadding * 2;
            Height = C.RouRaduis * 2 + C.RouPadding * 2;

            StoryboardIn = this.TryFindResource("StoryboardIn") as Storyboard;
            StoryboardOut = this.TryFindResource("StoryboardOut") as Storyboard;
            initSector(actions);
            defaultSectorCache = cacheSector();
            StoryboardOut.Begin();
            StoryboardOut.Pause();
            StoryboardIn.Begin();
            StoryboardIn.Pause();

            _disabled = true;
            Enable();
            _shown = true;
            HideRou();
        }


        private IntPtr CurrenthWnd;
        private void HookEx_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (!_shown)
            {
                this.Hide();
                CurrenthWnd = API.GetWindowFromPoint();
                this.Show();
                if (CurrenthWnd != IntPtr.Zero)
                {
                    var text = API.GetWindowText(CurrenthWnd);
                    var name = API.GetWindowName(CurrenthWnd);
                    var processName = System.IO.Path.GetFileNameWithoutExtension(name);
#if DEBUG
                    debugLabel.Content = processName;
#endif
                    initSector(processName);

                    if (config.HasApp(processName))
                    {
                        subfuncsLabel.Visibility = Visibility.Visible;
                        subfuncsLabel.Content = FirstLetterToUpper(processName);
                    }
                    else
                    {
                        subfuncsLabel.Visibility = Visibility.Collapsed;
                    }
                }
                ShowRou();
            }
            e.Handled = true;
        }
        private void HookEx_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (currentAction != null)
            {
                if (CurrenthWnd != IntPtr.Zero && CurrenthWnd != hWnd)
                    API.InvokeWindow(CurrenthWnd);
                if (currentAction.HoverRelease() != false)
                    HideRou();
            }
            else
                HideRou();
            e.Handled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            notifyIcon.Visible = true;
        }

        public void Enable()
        {
            _disabled = false;
            hookEx.hook();
        }

        public void Disable()
        {
            _disabled = true;
            hookEx.unhook();
            HideRou();
        }

        public bool ShowRou()
        {
            if (!_disabled && !_shown)
            {
                _shown = true;
                API.UnsetWindowTrans(hWnd);
                Opacity = 0;
                ShowByMouse();
                currentAction = null;

                StoryboardIn.Seek(TimeSpan.FromMilliseconds(1), TimeSeekOrigin.BeginTime);
                StoryboardIn.Resume();

                StoryboardOut.Pause();
                StoryboardOut.Seek(TimeSpan.FromMilliseconds(1), TimeSeekOrigin.BeginTime);
                Opacity = 1;
                IsHitTestVisible = true;
                return true;
            }
            return false;
        }

        public void HideRou()
        {
            if (_shown)
            {
                API.SetWindowTrans(hWnd);
                IsHitTestVisible = false;
                _shown = false;
                StoryboardOut.Resume();
                StoryboardIn.Pause();
            }
        }


        public List<UIElement> cacheSector()
        {
            List<UIElement> children = new List<UIElement>();
            foreach (UIElement c in cavans.Children)
            {
                children.Add(c);
            }
            return children;
        }

        public void cacheSector(string name) {
            sectorCache.Add(name, cacheSector());
        }

        public void applySectorCache(List<UIElement> children) {
            cavans.Children.Clear();
            foreach (var c in children)
                cavans.Children.Add(c);
        }

        public bool applySectorCache(string name)
        {
            if (sectorCache.ContainsKey(name))
            {
                List<UIElement> children = sectorCache[name];
                applySectorCache(children);
                return true;
            }
            else
                return false;
        }

        public void initSector(string processName)
        {
            //if (!applySectorCache(processName)) {
                var actions = config.LoadActionsForApp(processName);
                initSector(actions);
            //cacheSector(processName);
            //}
        }

        public void initSector(List<Action> actions)
        {
            cavans.Children.Clear();

            int count = actions.Count;

            double sectorTheta = Math.PI * 2 / count;
            double offest = -Math.PI / 2 - sectorTheta / 2;
            double iconRaduis = (C.RouRaduis + C.RouInnderRaduis) / 2;
            for (int i = 0; i < count; i++)
            {
                var action = actions[i];

                var strokeColor = action.Active && false ? C.RouActiveStrokeBrush : C.RouStrokeBrush;

                Path path = CreateSector(C.RouRaduis, C.RouInnderRaduis, offest + sectorTheta * i, offest + sectorTheta * (i + 1), C.RouBackBrush, strokeColor, 1);
                var icon = action.Icon;
                cavans.Children.Add(path);
                cavans.Children.Add(icon);

                path.Tag = action;
                path.Cursor = System.Windows.Input.Cursors.Hand;
                path.MouseEnter += Path_MouseEnter;
                path.MouseLeave += Path_MouseLeave;

                icon.Width = C.RouIconSize;
                icon.Height = C.RouIconSize;
                icon.Foreground = C.RouActionIconBrush;
                icon.IsHitTestVisible = false;

                var iconPos = PolarToRect(iconRaduis, (i + 0.5) * sectorTheta + offest);
                Canvas.SetLeft(path, C.RouRaduis);
                Canvas.SetTop(path, C.RouRaduis);
                Canvas.SetLeft(icon, C.RouRaduis - C.RouIconSize / 2 + iconPos.X);
                Canvas.SetTop(icon, C.RouRaduis - C.RouIconSize / 2 + iconPos.Y - (ShowText ? 10 : 0));

                if (ShowText)
                {
                    var label = new TextBlock();
                    label.Text = action.Text;
                    label.Foreground = C.RouActionIconTextBrush;
                    label.VerticalAlignment = VerticalAlignment.Top;
                    label.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    label.FontSize = 9;
                    label.TextTrimming = TextTrimming.CharacterEllipsis;
                    label.TextAlignment = TextAlignment.Center;
                    label.Width = C.RouIconSize / 2 * 3;
                    cavans.Children.Add(label);
                    Canvas.SetLeft(label, C.RouRaduis + iconPos.X - label.Width / 2);
                    Canvas.SetTop(label, C.RouRaduis + iconPos.Y + 5);
                }
            }
        }



        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            API.SetWindowNoactive(this);
        }


        public void ShowAtPosition(double x, double y)
        {
            // Get the dpi of the screen
            Matrix m = PresentationSource.FromVisual(System.Windows.Application.Current.MainWindow).CompositionTarget.TransformToDevice;
            double dx = m.M11;
            double dy = m.M22;

            Left = x / dx - C.RouRaduis - C.RouPadding;
            Top = y / dy - C.RouRaduis - C.RouPadding;
        }

        public void ShowByMouse()
        {
            Point point = MouseCursor.GetMousePosition();
            ShowAtPosition(point.X, point.Y);
        }

        private static Point PolarToRect(double r, double theta)
        {
            return new Point(Math.Cos(theta) * r, Math.Sin(theta) * r);
        }

        private Path CreateSector(double outerRadius, double innerRadius, double fromDegree, double toDegree, Brush fill, Brush stroke, double strokeThickness)
        {
            if (fromDegree > toDegree)
            {
                var temp = toDegree;
                toDegree = fromDegree;
                fromDegree = temp;
            }

            var p1 = PolarToRect(outerRadius, fromDegree);
            var p2 = PolarToRect(innerRadius, fromDegree);
            var p3 = PolarToRect(innerRadius, toDegree);
            var p4 = PolarToRect(outerRadius, toDegree);

            var path = new Path();
            var largeArc = (toDegree - fromDegree) >= Math.PI;

            var geo = new PathGeometry(new List<PathFigure>() {
                new PathFigure(p1, new List<PathSegment>() {
                    new LineSegment(p2, false),
                    new ArcSegment(p3, new Size(innerRadius, innerRadius), 0, largeArc, SweepDirection.Clockwise, true),
                    new LineSegment(p4, false),
                    new ArcSegment(p1, new Size(outerRadius, outerRadius), 0, largeArc, SweepDirection.Counterclockwise, true),
                }, true)
            });

            path.Data = geo;
            path.Fill = fill;
            path.Stroke = stroke;
            path.StrokeThickness = strokeThickness;
            path.Opacity = C.RouSectorOpacity;

            return path;
        }

        private void Path_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var path = (Path)sender;
            (path.Tag as Action)?.HoverRelease();
            HideRou();
        }

        private void Path_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var path = (Path)sender;
            path.Opacity = C.RouSectorOpacity;
            (path.Data as PathGeometry).Figures[0].Segments[0].IsStroked = false;
            (path.Data as PathGeometry).Figures[0].Segments[2].IsStroked = false;
            currentAction = null;
        }

        private void Path_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var path = (Path)sender;
            path.Opacity = C.RouSectorActiveOpacity;
            (path.Data as PathGeometry).Figures[0].Segments[0].IsStroked = true;
            (path.Data as PathGeometry).Figures[0].Segments[2].IsStroked = true;
            currentAction = path.Tag as Action;
        }

        public void Exit()
        {
            Disable();
            notifyIcon.Visible = false;
            Environment.Exit(0);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private void subfuncsLabel_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            hoveredOnSubfuncLabel = true;
            EasyTimer.SetTimeout(() =>
            {
                if (hoveredOnSubfuncLabel)
                {
                    initSector("_default");
                    subfuncsLabel.Content = "Default";
                }
                return null;
            }, 400);
        }

        private void subfuncsLabel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            hoveredOnSubfuncLabel = false;
        }

        public string FirstLetterToUpper(string str)
        {
            if (str == null)
                return null;

            if (str.Length > 1)
                return char.ToUpper(str[0]) + str.Substring(1);

            return str.ToUpper();
        }
    }
}