// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Forms
{
    using Classes;
    using Properties;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Windows.Forms;

    /// <inheritdoc/>
    public partial class ChannelsForm : Form
    {
        /// <summary>The string placeholder for the search text box</summary>
        private const string SearchTextFieldPlaceholder = "Search...";

        /// <summary>Is the channel editor initialized yet</summary>
        private bool _isInitialized;

        /// <summary>Are we changing the channel so we don't trigger our own events</summary>
        private bool _isChannelChanging;

        /// <summary>The current filter for the channels</summary>
        private string _allChannelsFilter;

        /// <summary>The name of the filter for all the channels to display</summary>
        private string _allChannelCategoryFilter = "None";

        /// <summary>Are we filtering channels based on a search term</summary>
        private bool _isAllChannelsSearchFiltered => !string.IsNullOrWhiteSpace(_allChannelsFilter);

        /// <inheritdoc/>
        public ChannelsForm()
        {
            InitializeComponent();

            Shown += (sender, args) => Initialize();
        }

        /// <summary>Initialize the channel editor</summary>
        private void Initialize()
        {
            LoadAll();

            treeViewAllChannels.AfterSelect += (s, a) =>
            {
                if (_isChannelChanging || !_isInitialized)
                {
                    return;
                }

                if (a.Node.Name == "NodeRoot" || a.Node.Name.StartsWith("NodeCat"))
                {
                    if (a.Node.Name.StartsWith("NodeCat"))
                    {
                        a.Node.Expand();

                        treeViewAllChannels.SelectedNode = treeViewAllChannels.Nodes[0].Nodes[a.Node.Name].Nodes[0];
                    }

                    UpdateGui();
                }
                else
                {
                    TvCore.SetChannel(uint.Parse(a.Node.Name));
                }
            };

            treeViewFavoriteChannels.AfterSelect += (s, a) => buttonFavoriteRemove.Enabled = a.Node.Name != "NodeRoot";

            treeViewFavoriteChannels.NodeMouseDoubleClick += (s, a) =>
            {
                if (_isChannelChanging || !_isInitialized)
                {
                    return;
                }

                TvCore.SetChannel(uint.Parse(a.Node.Name));
            };

            TvCore.ChannelChanged += newChannel =>
            {
                if (!_isInitialized)
                {
                    return;
                }

                _isChannelChanging = true;

                UpdateGui();
                    
                _isChannelChanging = false;
            };

            _isInitialized = true;
        }

        /// <summary>Update the gui with data from both the channels and favorites system</summary>
        private void UpdateGui()
        {
            this.InvokeIfRequired(() =>
            {
                var currentChannelIndex = TvCore.CurrentChannelIndex;

                if (!_isAllChannelsSearchFiltered)
                {
                    treeViewAllChannels.SelectedNode = treeViewAllChannels.Nodes[0].Nodes[currentChannelIndex.ToString()];
                }

                labelChannelName.Text = string.Format(Resources.ChannelsForm_ChannelNameLabel, TvCore.CurrentChannel.Index, TvCore.CurrentChannel.Name.Replace(": ", "\n"));

                if (TvCore.CurrentChannel.Logo == null)
                {
                    pictureBoxChannelLogo.Image = Resources.iptv.ToBitmap();
                }
                else
                {
                    pictureBoxChannelLogo.ImageLocation = TvCore.CurrentChannel.Logo.ToString();
                }

                if (TvCore.ChannelFavorites.Count == 0)
                {
                    buttonFavoriteRemove.Enabled = false;
                }
                else
                {
                    buttonFavoriteRemove.Enabled = treeViewFavoriteChannels.SelectedNode != null;
                }
            });
        }

        /// <summary>Load all the data and update the gui all from another thread</summary>
        private void LoadAll()
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                LoadChannels();
                LoadFavoriteChannels();
                UpdateGui();
            });
        }

        /// <summary>Load the channel data for display</summary>
        private void LoadChannels()
        {
            var tvChannels = TvCore.Channels;

            if (!string.IsNullOrWhiteSpace(_allChannelsFilter))
            {
                tvChannels = TvCore.Channels.FindAll(x => x.Name.ToLower().Contains(_allChannelsFilter.ToLower()));
            }

            if (_allChannelCategoryFilter != "None")
            {
                var tvChannelsCopy = tvChannels.ToList();

                var tvChannelCategories = tvChannels.Select(x =>
                {
                    var split = x.Name.Trim().Split(new[] {':'}, 2, StringSplitOptions.RemoveEmptyEntries);

                    var countryString = split.Length == 2 ? split[0] : string.Empty;

                    return countryString.Contains(" ") ? string.Empty : countryString.ToUpper();
                }).Distinct().Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                tvChannelCategories.Sort();

                var tmpTreeNode = new TreeNode("NodeRoot") { Name = "NodeRoot", Text = string.Format(Resources.ChannelsForm_CategoryNodeTitle, _allChannelCategoryFilter, tvChannelCategories.Count) };

                foreach (var channelCat in tvChannelCategories)
                {
                    var tvChannelByCat = tvChannels.FindAll(x => x.Name.ToLower().StartsWith(channelCat.ToLower() + ":"));

                    var tmpCatTreeNode = new TreeNode($"NodeCat{channelCat}") { Name = $"NodeCat{channelCat}", Text = string.Format(Resources.ChannelsForm_CategoryNodeTitle, channelCat, tvChannelByCat.Count) };

                    foreach (var channel in tvChannelByCat)
                    {
                        tmpCatTreeNode.Nodes.Add(TvCore.ChannelIndexList.IndexOf(channel.Index).ToString(), $"{channel.Index} {channel.Name}");
                        tvChannelsCopy.Remove(channel);
                    }

                    tmpTreeNode.Nodes.Add(tmpCatTreeNode);
                }

                if (tvChannelsCopy.Any())
                {
                    var naCatTreeNode = new TreeNode("NodeCatNA") { Name = "NodeCatNA", Text = string.Format(Resources.ChannelsForm_CategoryNotAvailableTitle, tvChannelsCopy.Count) };

                    foreach (var channel in tvChannelsCopy.ToList())
                    {
                        naCatTreeNode.Nodes.Add(TvCore.ChannelIndexList.IndexOf(channel.Index).ToString(), $"{channel.Index} {channel.Name}");
                        tvChannelsCopy.RemoveAll(x => x.Index == channel.Index);
                    }

                    tmpTreeNode.Nodes.Add(naCatTreeNode);
                }

                this.InvokeIfRequired(() =>
                {
                    treeViewAllChannels.Nodes.Clear();
                    treeViewAllChannels.Nodes.Add(tmpTreeNode);
                    treeViewAllChannels.Nodes[0].Expand();
                });
            }
            else
            {
                var tmpTreeNode = new TreeNode("NodeRoot") { Name = "NodeRoot", Text = string.Format(Resources.ChannelsForm_AllChannelsTitle, tvChannels.Count) };

                foreach (var channel in tvChannels)
                {
                    tmpTreeNode.Nodes.Add(TvCore.ChannelIndexList.IndexOf(channel.Index).ToString(), $"{channel.Index} {channel.Name}");
                }

                this.InvokeIfRequired(() =>
                {
                    treeViewAllChannels.Nodes.Clear();
                    treeViewAllChannels.Nodes.Add(tmpTreeNode);
                    treeViewAllChannels.Nodes[0].Expand();

                    UpdateGui();
                });
            }
        }

        /// <summary>Load the favorite channels data for display</summary>
        private void LoadFavoriteChannels()
        {
            var channelFavoritesCount = TvCore.ChannelFavorites.Count;

            if (channelFavoritesCount == 0)
            {
                return;
            }

            var tmpTreeNode = new TreeNode("NodeRoot") { Name = "NodeRoot", Text = string.Format(Resources.ChannelsForm_FavoritesRootTitle, channelFavoritesCount) };

            foreach (var channelId in TvCore.ChannelFavorites)
            {
                var channel = TvCore.Channels.Find(x => x.Id == channelId);

                tmpTreeNode.Nodes.Add(TvCore.ChannelIndexList.IndexOf(channel.Index).ToString(), $"{channel.Index} {channel.Name}");
            }

            this.InvokeIfRequired(() =>
            {
                treeViewFavoriteChannels.Nodes.Clear();
                treeViewFavoriteChannels.Nodes.Add(tmpTreeNode);
                treeViewFavoriteChannels.Nodes[0].Expand();
            });
        }

        /// <summary>The <see cref="Form"/> close event handler, we don't want to dispose of this window</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
        private void ChannelsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing)
            {
                return;
            }

            e.Cancel = true;

            TvCore.Settings.ChannelEditorOpen = false;
            TvCore.Settings.Save();

            Hide();
        }

        /// <summary>Focus handler for the <see cref="TextBox"/>, checks for and removes the placeholder text</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
        private void TextBoxAllChannelsSearch_Enter(object sender, EventArgs e)
        {
            if (textBoxAllChannelsSearch.Text == SearchTextFieldPlaceholder)
            {
                textBoxAllChannelsSearch.Text = string.Empty;
            }
        }

        /// <summary>Focus lost handler for the <see cref="TextBox"/>, checks for text and adds the placeholder text if empty</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
        private void TextBoxAllChannelsSearch_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxAllChannelsSearch.Text))
            {
                textBoxAllChannelsSearch.Text = SearchTextFieldPlaceholder;
            }
        }

        /// <summary>A <see cref="TextBox"/> text changed handler, used to filter the channels</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
        private void TextBoxAllChannelsSearch_TextChanged(object sender, EventArgs e)
        {
            if (textBoxAllChannelsSearch.Text == SearchTextFieldPlaceholder)
            {
                return;
            }

            _allChannelsFilter = !string.IsNullOrWhiteSpace(textBoxAllChannelsSearch.Text) ? textBoxAllChannelsSearch.Text : string.Empty;

            LoadAll();
        }

        /// <summary>a <see cref="Button"/> click handler, used to change the category we are displaying channels under</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
        private void ButtonFilter_Click(object sender, EventArgs e)
        {
            if (!(sender is Button button))
            {
                return;
            }

            var buttonTag = (string) button.Tag;

            _allChannelCategoryFilter = buttonTag;

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (buttonTag)
            {
                case "None":
                {
                    buttonFilterCountries.Enabled = true;
                    buttonFilterNone.Enabled = false;
                }
                break;

                case "Countries":
                {
                    buttonFilterCountries.Enabled = false;
                    buttonFilterNone.Enabled = true;
                }
                break;
            }

            LoadAll();
        }

        /// <summary>A <see cref="Button"/> click handler, used to add a favorite channel</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
        private void ButtonFavoriteAdd_Click(object sender, EventArgs e)
        {
            var channelIdx = int.Parse(treeViewAllChannels.SelectedNode.Name);

            var channel = TvCore.Channels.Find(x => x.Index == TvCore.ChannelIndexList[channelIdx]);

            if (channel == null)
            {
                TvCore.LogError("[.NET] Channel was not found when adding to favorite channels");
                return;
            }

            TvCore.AddFavoriteChannel(channel.Id);

            LoadAll();
        }

        /// <summary>A <see cref="Button"/> click handler, used to remove a favorite from the favorite channels list</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
        private void ButtonFavoriteRemove_Click(object sender, EventArgs e)
        {
            var channelIdx = int.Parse(treeViewFavoriteChannels.SelectedNode.Name);

            var channel = TvCore.Channels.Find(x => x.Index == TvCore.ChannelIndexList[channelIdx]);

            if (channel == null)
            {
                TvCore.LogError("[.NET] Channel was not found when removing a favorite channel");
                return;
            }

            TvCore.RemoveFavoriteChannel(channel.Id);

            LoadAll();
        }
    }
}
