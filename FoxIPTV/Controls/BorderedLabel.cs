// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Controls
{
    using System.Drawing;
    using System.Windows.Forms;

    /// <summary>A label that can have a border</summary>
    public class BorderedLabel : Label
    {
        /// <summary>Enable or disable the Left border</summary>
        public bool LeftBorder { get; set; }

        /// <summary>The width of the Left border</summary>
        public int LeftBorderWidth { get; set; } = 1;

        /// <summary>The color of the Left border</summary>
        public Color LeftBorderColor { get; set; } = Color.Black;

        /// <summary>The style of the Left border</summary>
        public ButtonBorderStyle LeftBorderStyle { get; set; } = ButtonBorderStyle.Solid;

        /// <summary>Enable or disable the Top border</summary>
        public bool TopBorder { get; set; }

        /// <summary>The width of the Top border</summary>
        public int TopBorderWidth { get; set; } = 1;

        /// <summary>The color of the Top border</summary>
        public Color TopBorderColor { get; set; } = Color.Black;

        /// <summary>The style of the Top border</summary>
        public ButtonBorderStyle TopBorderStyle { get; set; } = ButtonBorderStyle.Solid;

        /// <summary>Enable or disable the Right border</summary>
        public bool RightBorder { get; set; }

        /// <summary>The width of the Right border</summary>
        public int RightBorderWidth { get; set; } = 1;

        /// <summary>The color of the Right border</summary>
        public Color RightBorderColor { get; set; } = Color.Black;

        /// <summary>The style of the Right border</summary>
        public ButtonBorderStyle RightBorderStyle { get; set; } = ButtonBorderStyle.Solid;

        /// <summary>Enable or disable the Bottom border</summary>
        public bool BottomBorder { get; set; }

        /// <summary>The width of the Bottom border</summary>
        public int BottomBorderWidth { get; set; } = 1;

        /// <summary>The color of the Bottom border</summary>
        public Color BottomBorderColor { get; set; } = Color.Black;

        /// <summary>The style of the Bottom border</summary>
        public ButtonBorderStyle BottomBorderStyle { get; set; } = ButtonBorderStyle.Solid;

        /// <summary>Enable or disable the hover effect for the label</summary>
        public bool HoverEffect { get; set; }

        /// <summary>Save the old color to change back to when mouse moves out of the label</summary>
        private Color _oldColor;

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
