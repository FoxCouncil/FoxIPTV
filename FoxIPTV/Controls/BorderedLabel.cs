// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Controls
{
    using System.Drawing;
    using System.Windows.Forms;

    public class BorderedLabel : Label
    {
        public bool LeftBorder { get; set; }

        public int LeftBorderWidth { get; set; } = 1;

        public Color LeftBorderColor { get; set; } = Color.Black;

        public ButtonBorderStyle LeftBorderStyle { get; set; } = ButtonBorderStyle.Solid;

        public bool TopBorder { get; set; }

        public int TopBorderWidth { get; set; } = 1;

        public Color TopBorderColor { get; set; } = Color.Black;

        public ButtonBorderStyle TopBorderStyle { get; set; } = ButtonBorderStyle.Solid;

        public bool RightBorder { get; set; }

        public int RightBorderWidth { get; set; } = 1;

        public Color RightBorderColor { get; set; } = Color.Black;

        public ButtonBorderStyle RightBorderStyle { get; set; } = ButtonBorderStyle.Solid;

        public bool BottomBorder { get; set; }

        public int BottomBorderWidth { get; set; } = 1;

        public Color BottomBorderColor { get; set; } = Color.Black;

        public ButtonBorderStyle BottomBorderStyle { get; set; } = ButtonBorderStyle.Solid;

        public bool HoverEffect { get; set; }

        private Color _oldColor;

        public BorderedLabel()
        {
            UseMnemonic = false;

            MouseEnter += (s, a) =>
            {
                if (!HoverEffect)
                {
                    return;
                }

                _oldColor = BackColor;
                BackColor = ControlPaint.Light(BackColor);
            };

            MouseLeave += (s, a) =>
            {
                if (!HoverEffect)
                {
                    return;
                }

                BackColor = _oldColor;
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var gfx = e.Graphics;

            var leftWidth = LeftBorder ? LeftBorderWidth : 0;
            var topWidth = TopBorder ? TopBorderWidth : 0;
            var rightWidth = RightBorder ? RightBorderWidth : 0;
            var bottomWidth = BottomBorder ? BottomBorderWidth : 0;

            var borderRectangle = ClientRectangle;

            ControlPaint.DrawBorder(gfx, borderRectangle, 
                LeftBorderColor,
                leftWidth,
                LeftBorderStyle,
                TopBorderColor,
                topWidth,
                TopBorderStyle,
                RightBorderColor,
                rightWidth,
                RightBorderStyle,
                BottomBorderColor,
                bottomWidth,
                BottomBorderStyle);
        }
    }
}
