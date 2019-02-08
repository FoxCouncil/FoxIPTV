// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Forms
{
    using System;
    using System.Threading;
    using System.Windows.Forms;
    using Classes;
    using System.Linq;
    using Properties;

    public partial class ChannelsForm : Form
    {
        private const string SearchTextFieldPlaceholder = "Search...";

        private bool _isInitialized;

        private bool _isChannelChanging = false;

        private string _allChannelsFilter;

        private string _allChannelCategoryFilter = "None";

        private bool _isAllChannelsSearchFiltered => !string.IsNullOrWhiteSpace(_allChannelsFilter);

        public ChannelsForm()
        {
            InitializeComponent();

            Shown += (sender, args) => Initialize();
        }

        private void Initialize()
        {
            LoadAll();

            treeViewAllChannels.AfterSelect += (sender, args) =>
            {
                if (_isChannelChanging)
                {
                    return;
                }

                if (!_isInitialized)
                {
                    return;
                }

                if (args.Node.Name == "NodeRoot" || args.Node.Name.StartsWith("NodeCat"))
                {
                    if (args.Node.Name.StartsWith("NodeCat"))
                    {
                        args.Node.Expand();

                        treeViewAllChannels.SelectedNode = treeViewAllChannels.Nodes[0].Nodes[args.Node.Name].Nodes[0];
                    }

                    UpdateGui();
                }
                else
                {
                    TvCore.SetChannel(uint.Parse(args.Node.Name));
                }
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

        private void UpdateGui()
        {
            this.InvokeIfRequired(() =>
            {
                var currentChannelIndex = TvCore.CurrentChannelIndex;

                if (!_isAllChannelsSearchFiltered)
                {
                    treeViewAllChannels.SelectedNode = treeViewAllChannels.Nodes[0].Nodes[currentChannelIndex.ToString()];
                }

                labelChannelName.Text = $"{TvCore.CurrentChannel.Index}\n{TvCore.CurrentChannel.Name.Replace(": ", "\n")}";

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

        private void LoadAll()
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                LoadChannels();
                LoadFavoriteChannels();
                UpdateGui();
            });
        }

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

                var tmpTreeNode = new TreeNode("NodeRoot") { Name = "NodeRoot", Text = $"{_allChannelCategoryFilter} ({tvChannelCategories.Count})" };

                foreach (var channelCat in tvChannelCategories)
                {
                    var tvChannelByCat = tvChannels.FindAll(x => x.Name.ToLower().StartsWith(channelCat.ToLower() + ":"));

                    var tmpCatTreeNode = new TreeNode($"NodeCat{channelCat}") { Name = $"NodeCat{channelCat}", Text = channelCat + $" ({tvChannelByCat.Count})" };

                    foreach (var channel in tvChannelByCat)
                    {
                        tmpCatTreeNode.Nodes.Add(TvCore.ChannelIndexList.IndexOf(channel.Index).ToString(), $"{channel.Index} {channel.Name}");
                        tvChannelsCopy.Remove(channel);
                    }

                    tmpTreeNode.Nodes.Add(tmpCatTreeNode);
                }

                if (tvChannelsCopy.Any())
                {
                    var naCatTreeNode = new TreeNode("NodeCatNA") { Name = "NodeCatNA", Text = $"N/A ({tvChannelsCopy.Count})" };

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
                var tmpTreeNode = new TreeNode("NodeRoot") { Name = "NodeRoot", Text = $"All Channels ({tvChannels.Count})" };

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

        private void LoadFavoriteChannels()
        {
            var channelFavoritesCount = TvCore.ChannelFavorites.Count;

            if (channelFavoritesCount == 0)
            {
                return;
            }

            var tmpTreeNode = new TreeNode("NodeRoot") { Name = "NodeRoot", Text = $"Favorite Channels ({channelFavoritesCount})" };

            foreach (var channelId in TvCore.ChannelFavorites)
            {
                var channel = TvCore.Channels.Find(x => x.Id == channelId);

                tmpTreeNode.Nodes.Add(channel.Index.ToString(), $"{channel.Index} {channel.Name}");
            }

            this.InvokeIfRequired(() =>
            {
                treeViewFavoriteChannels.Nodes.Clear();
                treeViewFavoriteChannels.Nodes.Add(tmpTreeNode);
                treeViewFavoriteChannels.Nodes[0].Expand();
            });
        }

        private void ChannelsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing)
            {
                return;
            }

            e.Cancel = true;

            Settings.Default.ChannelEditorOpen = false;
            Settings.Default.Save();

            Hide();
        }

        private void textBoxAllChannelsSearch_Enter(object sender, EventArgs e)
        {
            if (textBoxAllChannelsSearch.Text == SearchTextFieldPlaceholder)
            {
                textBoxAllChannelsSearch.Text = string.Empty;
            }
        }

        private void textBoxAllChannelsSearch_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxAllChannelsSearch.Text))
            {
                textBoxAllChannelsSearch.Text = SearchTextFieldPlaceholder;
            }
        }

        private void textBoxAllChannelsSearch_TextChanged(object sender, EventArgs e)
        {
            if (textBoxAllChannelsSearch.Text == SearchTextFieldPlaceholder)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(textBoxAllChannelsSearch.Text))
            {
                _allChannelsFilter = textBoxAllChannelsSearch.Text;
            }
            else
            {
                _allChannelsFilter = string.Empty;
            }

            LoadAll();
        }

        private void buttonFilter_Click(object sender, EventArgs e)
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

        private void buttonFavoriteAdd_Click(object sender, EventArgs e)
        {
            var channelId = uint.Parse(treeViewAllChannels.SelectedNode.Name);

            var channel = TvCore.Channels.Find(x => x.Index == channelId);

            if (channel == null)
            {
                MessageBox.Show("Oops");
            }
            else
            {
                TvCore.AddFavoriteChannel(channel.Id);

                LoadAll();
            }
        }
    }
}
