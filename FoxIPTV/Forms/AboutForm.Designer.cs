namespace FoxIPTV.Forms
{
    partial class AboutForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.Label label1;
            System.Windows.Forms.GroupBox groupBoxWhy;
            System.Windows.Forms.LinkLabel linkLabelIconAttribution;
            System.Windows.Forms.Label label2;
            this.buttonClose = new System.Windows.Forms.Button();
            label1 = new System.Windows.Forms.Label();
            groupBoxWhy = new System.Windows.Forms.GroupBox();
            linkLabelIconAttribution = new System.Windows.Forms.LinkLabel();
            label2 = new System.Windows.Forms.Label();
            groupBoxWhy.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            label1.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label1.Location = new System.Drawing.Point(12, 9);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(279, 23);
            label1.TabIndex = 1;
            label1.Text = "Fox IPTV";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBoxWhy
            // 
            groupBoxWhy.Controls.Add(label2);
            groupBoxWhy.Location = new System.Drawing.Point(12, 36);
            groupBoxWhy.Name = "groupBoxWhy";
            groupBoxWhy.Size = new System.Drawing.Size(279, 98);
            groupBoxWhy.TabIndex = 2;
            groupBoxWhy.TabStop = false;
            groupBoxWhy.Text = "Why?";
            // 
            // linkLabelIconAttribution
            // 
            linkLabelIconAttribution.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            linkLabelIconAttribution.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            linkLabelIconAttribution.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            linkLabelIconAttribution.Location = new System.Drawing.Point(13, 140);
            linkLabelIconAttribution.Margin = new System.Windows.Forms.Padding(3);
            linkLabelIconAttribution.Name = "linkLabelIconAttribution";
            linkLabelIconAttribution.Size = new System.Drawing.Size(278, 20);
            linkLabelIconAttribution.TabIndex = 3;
            linkLabelIconAttribution.TabStop = true;
            linkLabelIconAttribution.Text = "Some icons by Yusuke Kamiyamane";
            linkLabelIconAttribution.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            linkLabelIconAttribution.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelIconAttribution_LinkClicked);
            // 
            // buttonClose
            // 
            this.buttonClose.Location = new System.Drawing.Point(12, 166);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(280, 23);
            this.buttonClose.TabIndex = 0;
            this.buttonClose.Text = "Close";
            this.buttonClose.UseVisualStyleBackColor = true;
            this.buttonClose.Click += new System.EventHandler(this.buttonClose_Click);
            // 
            // label2
            // 
            label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label2.Location = new System.Drawing.Point(7, 20);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(266, 58);
            label2.TabIndex = 0;
            label2.Text = "BECAUSE";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // AboutForm
            // 
            this.AcceptButton = this.buttonClose;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(304, 201);
            this.Controls.Add(linkLabelIconAttribution);
            this.Controls.Add(groupBoxWhy);
            this.Controls.Add(label1);
            this.Controls.Add(this.buttonClose);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Fox IPTV - About";
            this.TopMost = true;
            groupBoxWhy.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        

        private System.Windows.Forms.Button buttonClose;
    }
}