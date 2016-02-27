﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LeagueClient.Logic;
using LeagueClient.Logic.Chat;
using LeagueClient.Logic.Queueing;
using MFroehlich.League.Assets;
using RtmpSharp.Messaging;
using LeagueClient.UI.Main.Lobbies;
using RiotClient.Riot.Platform;
using RiotClient.Lobbies;
using RiotClient;

namespace LeagueClient.UI.Main.Custom {
  /// <summary>
  /// Interaction logic for CustomLobbyPage.xaml
  /// </summary>
  public sealed partial class CustomLobbyPage : Page, IClientSubPage {
    public event EventHandler Close;

    public GameDTO GameDto { get; private set; }

    private CustomLobby lobby;
    private ChatRoom chatRoom;
    private bool hasStarted;

    #region Constructors

    public CustomLobbyPage(CustomLobby lobby) {
      InitializeComponent();

      this.lobby = lobby;

      lobby.MemberJoined += Lobby_MemberJoined;
      lobby.MemberChangedTeam += Lobby_MemberChangedTeam;
      lobby.MemberLeft += Lobby_MemberLeft;
      lobby.LeftLobby += Lobby_LeftLobby;
      lobby.GotGameDTO += Lobby_GotGameDTO;
      lobby.Loaded += Lobby_Loaded;

      lobby.EnteredChampSelect += Lobby_EnteredChampSelect;

      lobby.CatchUp();

      chatRoom = new ChatRoom(lobby, SendBox, ChatHistory, ChatSend, ChatScroller);
      Session.Current.ChatManager.Status = ChatStatus.hostingPracticeGame;
    }

    private void Lobby_Loaded(object sender, EventArgs e) {
      Dispatcher.Invoke(() => {
        StartButt.Visibility = lobby.IsCaptain ? Visibility.Visible : Visibility.Collapsed;
        LoadingGrid.Visibility = Visibility.Collapsed;
      });
    }

    private void Lobby_GotGameDTO(object sender, GameDTO game) {
      Dispatcher.Invoke(() => {
        var map = GameMap.Maps.FirstOrDefault(m => m.MapId == game.MapId);

        MapImage.Source = new BitmapImage(GameMap.Images[map]);
        MapLabel.Content = map.DisplayName;
        ModeLabel.Content = GameMode.Values[game.GameMode];
        QueueLabel.Content = GameConfig.Values[game.GameTypeConfigId];
        TeamSizeLabel.Content = $"{game.MaxNumPlayers / 2}v{game.MaxNumPlayers / 2}";
      });
    }

    private void Lobby_MemberJoined(object sender, MemberEventArgs e) {
      Dispatcher.Invoke(() => {
        var member = e.Member as CustomLobby.CustomLobbyMember;
        StackPanel stack;

        if (member.Team == 0) stack = BlueTeam;
        else if (member.Team == 1) stack = RedTeam;
        else throw new Exception("UNEXPECTED TEAM");

        var player = new LobbyPlayer(member);
        stack.Children.Add(player);

        if (member == lobby.Me) {
          RedJoin.Visibility = BlueJoin.Visibility = Visibility.Collapsed;
          if (member.Team != 0) BlueJoin.Visibility = Visibility.Visible;
          if (member.Team != 1) RedJoin.Visibility = Visibility.Visible;
        }

        Sort();
      });
    }

    private void Lobby_MemberChangedTeam(object sender, MemberEventArgs e) {
      Dispatcher.Invoke(() => {
        var member = e.Member as CustomLobby.CustomLobbyMember;
        StackPanel stack;
        StackPanel other;

        if (member.Team == 1) {
          stack = BlueTeam; other = RedTeam;
        } else if (member.Team == 0) {
          stack = RedTeam; other = BlueTeam;
        } else throw new Exception("UNEXPECTED TEAM");

        var player = stack.Children.Cast<LobbyPlayer>().FirstOrDefault(p => p.Member == e.Member);
        stack.Children.Remove(player);
        other.Children.Add(player);

        if (member == lobby.Me) {
          RedJoin.Visibility = BlueJoin.Visibility = Visibility.Collapsed;
          if (member.Team != 0) BlueJoin.Visibility = Visibility.Visible;
          if (member.Team != 1) RedJoin.Visibility = Visibility.Visible;
        }

        Sort();
      });
    }

    private void Lobby_MemberLeft(object sender, MemberEventArgs e) {
      Dispatcher.Invoke(() => {
        var member = e.Member as CustomLobby.CustomLobbyMember;
        StackPanel stack;

        if (member.Team == 0) stack = BlueTeam;
        else if (member.Team == 1) stack = RedTeam;
        else throw new Exception("UNEXPECTED TEAM");

        var player = stack.Children.Cast<LobbyPlayer>().FirstOrDefault(p => p.Member == e.Member);
        stack.Children.Remove(player);
      });
    }

    private void Lobby_LeftLobby(object sender, EventArgs e) {
      Close?.Invoke(this, new EventArgs());
    }

    private void Lobby_EnteredChampSelect(object sender, Game game) {
      hasStarted = true;
      Client.MainWindow.BeginChampionSelect(game);
    }

    private void Sort() {
      foreach (var panel in new[] { BlueTeam, RedTeam }) {
        var players = panel.Children.Cast<LobbyPlayer>().ToList();
        foreach (var player in players) {
          panel.Children.Remove(player);
          int index = lobby.GetIndex(player.Member);
          panel.Children.Insert(index, player);
        }
      }
    }

    #endregion

    #region UI Events

    private void RedJoin_Click(object sender, RoutedEventArgs e) {
      if (((CustomLobby.CustomLobbyMember) lobby.Me).Team == 2) {
        lobby.SwitchToPlayer(2);
      } else {
        lobby.SwitchTeams();
      }
    }

    private void BlueJoin_Click(object sender, RoutedEventArgs e) {
      if (((CustomLobby.CustomLobbyMember) lobby.Me).Team == 2) {
        lobby.SwitchToPlayer(1);
      } else {
        lobby.SwitchTeams();
      }
    }

    private void Spectate_Click(object sender, RoutedEventArgs e) {
      lobby.SwitchToObserver();
    }

    private void Start_Click(object sender, RoutedEventArgs e) {
      lobby.StartChampSelect();
    }

    private void Quit_Click(object sender, RoutedEventArgs e) {
      Close?.Invoke(this, new EventArgs());
      Dispose();
    }

    #endregion

    public Page Page => this;
    public void Dispose() {
      if (!hasStarted)
        lobby.Quit();
      Session.Current.ChatManager.Status = ChatStatus.outOfGame;
    }
  }
}
