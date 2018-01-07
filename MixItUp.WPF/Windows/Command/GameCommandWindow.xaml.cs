﻿using Mixer.Base.Util;
using MixItUp.Base;
using MixItUp.Base.Actions;
using MixItUp.Base.Commands;
using MixItUp.Base.ViewModel.User;
using MixItUp.WPF.Controls.Actions;
using MixItUp.WPF.Controls.Games;
using MixItUp.WPF.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace MixItUp.WPF.Windows.Command
{
    /// <summary>
    /// Interaction logic for GameCommandWindow.xaml
    /// </summary>
    public partial class GameCommandWindow : LoadingWindowBase
    {
        private GameCommandBase command;

        private ObservableCollection<GamesProbabilityControl> probabilityControls = new ObservableCollection<GamesProbabilityControl>();

        public GameCommandWindow() : this(null) { }

        public GameCommandWindow(GameCommandBase command)
        {
            this.command = command;

            InitializeComponent();

            this.Initialize(this.StatusBar);
        }

        public GameCommandBase GetExistingCommand() { return this.command; }

        protected override async Task OnLoaded()
        {
            this.GameLowestRoleAllowedComboBox.ItemsSource = ChatCommand.PermissionsAllowedValues;
            this.GameLowestRoleAllowedComboBox.SelectedIndex = 0;

            this.GameResultTypeComboBox.ItemsSource = EnumHelper.GetEnumNames<GameResultType>();

            this.CurrencySelector.ShowMaximumAmountOption();

            this.ResultsProbabilityListView.ItemsSource = this.probabilityControls;

            await this.GameStartedCommandControl.Initialize(this, null);
            await this.GameEndedCommandControl.Initialize(this, null);
            await this.UserJoinedCommandControl.Initialize(this, null);
            await this.NotEnoughUsersCommandControl.Initialize(this, null);

            if (this.command != null)
            {
                this.GameNameTextBox.Text = this.command.Name;
                this.GameChatCommandTextBox.Text = this.command.CommandsString;
                this.GameCooldownTextBox.Text = this.command.Cooldown.ToString();
                this.GameLowestRoleAllowedComboBox.SelectedItem = EnumHelper.GetEnumName(this.command.Permissions);
                this.RankSelector.SetCurrencyRequirement(this.command.RankRequirement);
                this.CurrencySelector.SetCurrencyRequirement(this.command.CurrencyRequirement);

                if (this.command is MultiPlayerGameCommand)
                {
                    MultiPlayerGameCommand multiplayerCommand = (MultiPlayerGameCommand)this.command;

                    this.GameLengthTextBox.Text = multiplayerCommand.GameLength.ToString();
                    this.GameMinimumParticipantsTextBox.Text = multiplayerCommand.MinimumParticipants.ToString();
                    this.GameResultTypeComboBox.SelectedItem = EnumHelper.GetEnumName(multiplayerCommand.ResultType);

                    await this.GameStartedCommandControl.Initialize(this, multiplayerCommand.GameStartedCommand);
                    await this.GameEndedCommandControl.Initialize(this, multiplayerCommand.GameEndedCommand);
                    await this.UserJoinedCommandControl.Initialize(this, multiplayerCommand.UserJoinedCommand);
                    await this.NotEnoughUsersCommandControl.Initialize(this, multiplayerCommand.NotEnoughUsersCommand);

                    this.IsMultiplayerToggleSwitch.IsChecked = true;
                }

                foreach (GameResultProbability probability in this.command.ResultProbabilities)
                {
                    this.probabilityControls.Add(new GamesProbabilityControl(this, probability));
                }
            }
            else
            {
                this.GameCooldownTextBox.Text = "0";
            }
        }

        public void DeleteProbability(GamesProbabilityControl probabilityControl)
        {
            this.probabilityControls.Remove(probabilityControl);
        }

        private IEnumerable<string> GetCommandStrings() { return new List<string>(this.GameChatCommandTextBox.Text.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)); }

        private void IsMultiplayerToggleSwitch_Checked(object sender, RoutedEventArgs e)
        {
            this.MultiplayerGrid.Visibility = this.IsMultiplayerToggleSwitch.IsChecked.GetValueOrDefault() ? Visibility.Visible : Visibility.Collapsed;
        }

        protected void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void AddProbabilityButton_Click(object sender, RoutedEventArgs e)
        {
            this.probabilityControls.Add(new GamesProbabilityControl(this));
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            await this.RunAsyncOperation(async () =>
            {
                if (string.IsNullOrEmpty(this.GameNameTextBox.Text))
                {
                    await MessageBoxHelper.ShowMessageDialog("Name is missing");
                    return;
                }

                if (this.GameLowestRoleAllowedComboBox.SelectedIndex < 0)
                {
                    await MessageBoxHelper.ShowMessageDialog("A permission level must be selected");
                    return;
                }

                if (!await this.CurrencySelector.Validate())
                {
                    return;
                }

                if (string.IsNullOrEmpty(this.GameChatCommandTextBox.Text))
                {
                    await MessageBoxHelper.ShowMessageDialog("Commands is missing");
                    return;
                }

                if (this.GameChatCommandTextBox.Text.Any(c => !Char.IsLetterOrDigit(c) && !Char.IsWhiteSpace(c)))
                {
                    await MessageBoxHelper.ShowMessageDialog("Commands can only contain letters and numbers");
                    return;
                }

                int cooldown = 0;
                if (!int.TryParse(this.GameCooldownTextBox.Text, out cooldown) || cooldown < 0)
                {
                    await MessageBoxHelper.ShowMessageDialog("Cooldown must be 0 or greater");
                    return;
                }

                foreach (PermissionsCommandBase command in ChannelSession.AllChatCommands)
                {
                    if (this.command != command && this.GameNameTextBox.Text.Equals(command.Name))
                    {
                        await MessageBoxHelper.ShowMessageDialog("There already exists a chat command with the same name");
                        return;
                    }
                }

                IEnumerable<string> commandStrings = this.GetCommandStrings();
                if (commandStrings.GroupBy(c => c).Where(g => g.Count() > 1).Count() > 0)
                {
                    await MessageBoxHelper.ShowMessageDialog("Each command string must be unique");
                    return;
                }

                foreach (PermissionsCommandBase command in ChannelSession.AllChatCommands)
                {
                    if (command.IsEnabled && this.GetExistingCommand() != command)
                    {
                        if (commandStrings.Any(c => command.Commands.Contains(c)))
                        {
                            await MessageBoxHelper.ShowMessageDialog("There already exists a chat command that uses one of the command strings you have specified");
                            return;
                        }
                    }
                }

                if (!await this.RankSelector.Validate())
                {
                    return;
                }

                if (!await this.CurrencySelector.Validate())
                {
                    return;
                }

                if (this.CurrencySelector.GetCurrencyRequirement() == null)
                {
                    await MessageBoxHelper.ShowMessageDialog("A currency must be provided");
                    return;
                }

                int gameLength = 0;
                int minParticipants = 0;

                if (this.IsMultiplayerToggleSwitch.IsChecked.GetValueOrDefault())
                {
                    if (!int.TryParse(this.GameLengthTextBox.Text, out gameLength) || gameLength < 0)
                    {
                        await MessageBoxHelper.ShowMessageDialog("Game length must be 0 or greater");
                        return;
                    }

                    if (!int.TryParse(this.GameMinimumParticipantsTextBox.Text, out minParticipants) || minParticipants < 1)
                    {
                        await MessageBoxHelper.ShowMessageDialog("Minimum participants must be 1 or greater");
                        return;
                    }

                    if (this.GameResultTypeComboBox.SelectedIndex < 0)
                    {
                        await MessageBoxHelper.ShowMessageDialog("A result type must be selected");
                        return;
                    }
                }

                if (this.probabilityControls.Count == 0)
                {
                    await MessageBoxHelper.ShowMessageDialog("At least one probability result must be specified");
                    return;
                }

                List<GameResultProbability> probabilityResults = new List<GameResultProbability>();
                foreach (GamesProbabilityControl probabilityControl in this.probabilityControls)
                {
                    if (!await probabilityControl.Validate())
                    {
                        return;
                    }
                    probabilityResults.Add(probabilityControl.GetResultProbability());
                }

                if (probabilityResults.Select(p => p.Probability).Sum() != 100.0)
                {
                    await MessageBoxHelper.ShowMessageDialog("All probability results must add up to a total of 100%");
                    return;
                }

                UserRole permissionsRole = EnumHelper.GetEnumValueFromString<UserRole>((string)this.GameLowestRoleAllowedComboBox.SelectedItem);

                if (this.command != null)
                {
                    ChannelSession.Settings.GameCommands.Remove(this.command);
                }

                if (this.IsMultiplayerToggleSwitch.IsChecked.GetValueOrDefault())
                {
                    this.command = new MultiPlayerGameCommand(this.GameNameTextBox.Text, this.GetCommandStrings(), permissionsRole, cooldown, this.CurrencySelector.GetCurrencyRequirement(),
                        this.RankSelector.GetCurrencyRequirement(), probabilityResults, gameLength, minParticipants,
                        EnumHelper.GetEnumValueFromString<GameResultType>((string)this.GameResultTypeComboBox.SelectedItem));

                    MultiPlayerGameCommand multiplayerCommand = (MultiPlayerGameCommand)this.command;
                    multiplayerCommand.GameStartedCommand = this.GameStartedCommandControl.GetCommand();
                    multiplayerCommand.GameEndedCommand = this.GameEndedCommandControl.GetCommand();
                    multiplayerCommand.UserJoinedCommand = this.UserJoinedCommandControl.GetCommand();
                    multiplayerCommand.NotEnoughUsersCommand = this.NotEnoughUsersCommandControl.GetCommand();
                }
                else
                {
                    this.command = new SinglePlayerGameCommand(this.GameNameTextBox.Text, this.GetCommandStrings(), permissionsRole, cooldown, this.CurrencySelector.GetCurrencyRequirement(),
                        this.RankSelector.GetCurrencyRequirement(), probabilityResults);
                }

                ChannelSession.Settings.GameCommands.Add(this.command);

                await ChannelSession.SaveSettings();

                this.Close();
            });
        }
    }
}
