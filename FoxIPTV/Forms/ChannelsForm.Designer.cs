namespace FoxIPTV.Forms
{
    partial class ChannelsForm
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
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("All Channels");
            System.Windows.Forms.TreeNode treeNode2 = new System.Windows.Forms.TreeNode("Favorite Channels");
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ChannelsForm));
            this.treeViewAllChannels = new System.Windows.Forms.TreeView();
            this.tableLayoutPanelLeft = new System.Windows.Forms.TableLayoutPanel();
            this.textBoxAllChannelsSearch = new System.Windows.Forms.TextBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonFilterNone = new System.Windows.Forms.Button();
            this.buttonFilterCountries = new System.Windows.Forms.Button();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanelRight = new System.Windows.Forms.TableLayoutPanel();
            this.treeViewFavoriteChannels = new System.Windows.Forms.TreeView();
            this.groupBoxChannelDetails = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.labelChannelName = new System.Windows.Forms.Label();
            this.pictureBoxChannelLogo = new System.Windows.Forms.PictureBox();
            this.flowLayoutPanelOptionButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonFavoriteAdd = new System.Windows.Forms.Button();
            this.buttonFavoriteRemove = new System.Windows.Forms.Button();
            this.tableLayoutPanelLeft.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.tableLayoutPanelRight.SuspendLayout();
            this.groupBoxChannelDetails.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxChannelLogo)).BeginInit();
            this.flowLayoutPanelOptionButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // treeViewAllChannels
            // 
            this.treeViewAllChannels.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewAllChannels.FullRowSelect = true;
            this.treeViewAllChannels.HideSelection = false;
            this.treeViewAllChannels.Location = new System.Drawing.Point(4, 110);
            this.treeViewAllChannels.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.treeViewAllChannels.Name = "treeViewAllChannels";
            treeNode1.Name = "NodeRoot";
            treeNode1.Text = "All Channels";
            this.treeViewAllChannels.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode1});
            this.treeViewAllChannels.ShowRootLines = false;
            this.treeViewAllChannels.Size = new System.Drawing.Size(399, 559);
            this.treeViewAllChannels.TabIndex = 0;
            // 
            // tableLayoutPanelLeft
            // 
            this.tableLayoutPanelLeft.AutoSize = true;
            this.tableLayoutPanelLeft.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanelLeft.ColumnCount = 1;
            this.tableLayoutPanelLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelLeft.Controls.Add(this.treeViewAllChannels, 0, 2);
            this.tableLayoutPanelLeft.Controls.Add(this.textBoxAllChannelsSearch, 0, 1);
            this.tableLayoutPanelLeft.Controls.Add(this.flowLayoutPanel1, 0, 0);
            this.tableLayoutPanelLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelLeft.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.FixedSize;
            this.tableLayoutPanelLeft.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelLeft.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tableLayoutPanelLeft.Name = "tableLayoutPanelLeft";
            this.tableLayoutPanelLeft.RowCount = 3;
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelLeft.Size = new System.Drawing.Size(407, 674);
            this.tableLayoutPanelLeft.TabIndex = 1;
            // 
            // textBoxAllChannelsSearch
            // 
            this.textBoxAllChannelsSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxAllChannelsSearch.Location = new System.Drawing.Point(4, 68);
            this.textBoxAllChannelsSearch.Margin = new System.Windows.Forms.Padding(4, 11, 4, 11);
            this.textBoxAllChannelsSearch.Name = "textBoxAllChannelsSearch";
            this.textBoxAllChannelsSearch.Size = new System.Drawing.Size(399, 26);
            this.textBoxAllChannelsSearch.TabIndex = 1;
            this.textBoxAllChannelsSearch.Text = "Search...";
            this.textBoxAllChannelsSearch.TextChanged += new System.EventHandler(this.textBoxAllChannelsSearch_TextChanged);
            this.textBoxAllChannelsSearch.Enter += new System.EventHandler(this.textBoxAllChannelsSearch_Enter);
            this.textBoxAllChannelsSearch.Leave += new System.EventHandler(this.textBoxAllChannelsSearch_Leave);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.AutoSize = true;
            this.flowLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowLayoutPanel1.Controls.Add(this.buttonFilterNone);
            this.flowLayoutPanel1.Controls.Add(this.buttonFilterCountries);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(4, 11);
            this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(4, 11, 4, 11);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(399, 35);
            this.flowLayoutPanel1.TabIndex = 2;
            // 
            // buttonFilterNone
            // 
            this.buttonFilterNone.Enabled = false;
            this.buttonFilterNone.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonFilterNone.Location = new System.Drawing.Point(0, 0);
            this.buttonFilterNone.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.buttonFilterNone.Name = "buttonFilterNone";
            this.buttonFilterNone.Size = new System.Drawing.Size(112, 35);
            this.buttonFilterNone.TabIndex = 0;
            this.buttonFilterNone.Tag = "None";
            this.buttonFilterNone.Text = "None";
            this.buttonFilterNone.UseVisualStyleBackColor = true;
            this.buttonFilterNone.Click += new System.EventHandler(this.buttonFilter_Click);
            // 
            // buttonFilterCountries
            // 
            this.buttonFilterCountries.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonFilterCountries.Location = new System.Drawing.Point(116, 0);
            this.buttonFilterCountries.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.buttonFilterCountries.Name = "buttonFilterCountries";
            this.buttonFilterCountries.Size = new System.Drawing.Size(112, 35);
            this.buttonFilterCountries.TabIndex = 1;
            this.buttonFilterCountries.Tag = "Countries";
            this.buttonFilterCountries.Text = "Countries";
            this.buttonFilterCountries.UseVisualStyleBackColor = true;
            this.buttonFilterCountries.Click += new System.EventHandler(this.buttonFilter_Click);
            // 
            // splitContainer
            // 
            this.splitContainer.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.tableLayoutPanelLeft);
            this.splitContainer.Panel1MinSize = 240;
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.tableLayoutPanelRight);
            this.splitContainer.Panel2MinSize = 240;
            this.splitContainer.Size = new System.Drawing.Size(936, 678);
            this.splitContainer.SplitterDistance = 411;
            this.splitContainer.SplitterWidth = 6;
            this.splitContainer.TabIndex = 2;
            // 
            // tableLayoutPanelRight
            // 
            this.tableLayoutPanelRight.AutoSize = true;
            this.tableLayoutPanelRight.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanelRight.BackColor = System.Drawing.Color.White;
            this.tableLayoutPanelRight.ColumnCount = 2;
            this.tableLayoutPanelRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanelRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRight.Controls.Add(this.treeViewFavoriteChannels, 1, 0);
            this.tableLayoutPanelRight.Controls.Add(this.groupBoxChannelDetails, 0, 0);
            this.tableLayoutPanelRight.Controls.Add(this.flowLayoutPanelOptionButtons, 0, 1);
            this.tableLayoutPanelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelRight.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelRight.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tableLayoutPanelRight.Name = "tableLayoutPanelRight";
            this.tableLayoutPanelRight.RowCount = 2;
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelRight.Size = new System.Drawing.Size(515, 674);
            this.tableLayoutPanelRight.TabIndex = 0;
            // 
            // treeViewFavoriteChannels
            // 
            this.treeViewFavoriteChannels.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewFavoriteChannels.FullRowSelect = true;
            this.treeViewFavoriteChannels.HideSelection = false;
            this.treeViewFavoriteChannels.Location = new System.Drawing.Point(184, 5);
            this.treeViewFavoriteChannels.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.treeViewFavoriteChannels.Name = "treeViewFavoriteChannels";
            treeNode2.Name = "NodeRoot";
            treeNode2.Text = "Favorite Channels";
            this.treeViewFavoriteChannels.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode2});
            this.tableLayoutPanelRight.SetRowSpan(this.treeViewFavoriteChannels, 2);
            this.treeViewFavoriteChannels.ShowRootLines = false;
            this.treeViewFavoriteChannels.Size = new System.Drawing.Size(327, 664);
            this.treeViewFavoriteChannels.TabIndex = 2;
            // 
            // groupBoxChannelDetails
            // 
            this.groupBoxChannelDetails.BackColor = System.Drawing.Color.White;
            this.groupBoxChannelDetails.Controls.Add(this.tableLayoutPanel1);
            this.groupBoxChannelDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxChannelDetails.Location = new System.Drawing.Point(4, 5);
            this.groupBoxChannelDetails.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupBoxChannelDetails.Name = "groupBoxChannelDetails";
            this.groupBoxChannelDetails.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupBoxChannelDetails.Size = new System.Drawing.Size(172, 327);
            this.groupBoxChannelDetails.TabIndex = 0;
            this.groupBoxChannelDetails.TabStop = false;
            this.groupBoxChannelDetails.Text = "Channel Details";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.labelChannelName, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.pictureBoxChannelLogo, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(4, 24);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(164, 298);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // labelChannelName
            // 
            this.labelChannelName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelChannelName.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelChannelName.Location = new System.Drawing.Point(4, 0);
            this.labelChannelName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelChannelName.Name = "labelChannelName";
            this.labelChannelName.Size = new System.Drawing.Size(156, 149);
            this.labelChannelName.TabIndex = 0;
            this.labelChannelName.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pictureBoxChannelLogo
            // 
            this.pictureBoxChannelLogo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBoxChannelLogo.Location = new System.Drawing.Point(4, 154);
            this.pictureBoxChannelLogo.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.pictureBoxChannelLogo.Name = "pictureBoxChannelLogo";
            this.pictureBoxChannelLogo.Size = new System.Drawing.Size(156, 139);
            this.pictureBoxChannelLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxChannelLogo.TabIndex = 1;
            this.pictureBoxChannelLogo.TabStop = false;
            // 
            // flowLayoutPanelOptionButtons
            // 
            this.flowLayoutPanelOptionButtons.AutoSize = true;
            this.flowLayoutPanelOptionButtons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowLayoutPanelOptionButtons.Controls.Add(this.buttonFavoriteAdd);
            this.flowLayoutPanelOptionButtons.Controls.Add(this.buttonFavoriteRemove);
            this.flowLayoutPanelOptionButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelOptionButtons.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanelOptionButtons.Location = new System.Drawing.Point(4, 342);
            this.flowLayoutPanelOptionButtons.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.flowLayoutPanelOptionButtons.Name = "flowLayoutPanelOptionButtons";
            this.flowLayoutPanelOptionButtons.Size = new System.Drawing.Size(172, 327);
            this.flowLayoutPanelOptionButtons.TabIndex = 1;
            // 
            // buttonFavoriteAdd
            // 
            this.buttonFavoriteAdd.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonFavoriteAdd.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.buttonFavoriteAdd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonFavoriteAdd.Location = new System.Drawing.Point(0, 0);
            this.buttonFavoriteAdd.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.buttonFavoriteAdd.Name = "buttonFavoriteAdd";
            this.buttonFavoriteAdd.Size = new System.Drawing.Size(171, 38);
            this.buttonFavoriteAdd.TabIndex = 1;
            this.buttonFavoriteAdd.Text = "Add To Favourites";
            this.buttonFavoriteAdd.UseVisualStyleBackColor = true;
            this.buttonFavoriteAdd.Click += new System.EventHandler(this.buttonFavoriteAdd_Click);
            // 
            // buttonFavoriteRemove
            // 
            this.buttonFavoriteRemove.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonFavoriteRemove.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.buttonFavoriteRemove.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonFavoriteRemove.Location = new System.Drawing.Point(0, 38);
            this.buttonFavoriteRemove.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.buttonFavoriteRemove.Name = "buttonFavoriteRemove";
            this.buttonFavoriteRemove.Size = new System.Drawing.Size(171, 38);
            this.buttonFavoriteRemove.TabIndex = 2;
            this.buttonFavoriteRemove.Text = "Remove Favourite";
            this.buttonFavoriteRemove.UseVisualStyleBackColor = true;
            // 
            // ChannelsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(936, 678);
            this.Controls.Add(this.splitContainer);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MinimumSize = new System.Drawing.Size(949, 708);
            this.Name = "ChannelsForm";
            this.Text = "Channel Editor - Fox IPTV";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ChannelsForm_FormClosing);
            this.tableLayoutPanelLeft.ResumeLayout(false);
            this.tableLayoutPanelLeft.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.tableLayoutPanelRight.ResumeLayout(false);
            this.tableLayoutPanelRight.PerformLayout();
            this.groupBoxChannelDetails.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxChannelLogo)).EndInit();
            this.flowLayoutPanelOptionButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        

        private System.Windows.Forms.TreeView treeViewAllChannels;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelLeft;
        private System.Windows.Forms.TextBox textBoxAllChannelsSearch;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button buttonFilterNone;
        private System.Windows.Forms.Button buttonFilterCountries;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRight;
        private System.Windows.Forms.GroupBox groupBoxChannelDetails;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label labelChannelName;
        private System.Windows.Forms.PictureBox pictureBoxChannelLogo;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelOptionButtons;
        private System.Windows.Forms.Button buttonFavoriteAdd;
        private System.Windows.Forms.TreeView treeViewFavoriteChannels;
        private System.Windows.Forms.Button buttonFavoriteRemove;
    }
}