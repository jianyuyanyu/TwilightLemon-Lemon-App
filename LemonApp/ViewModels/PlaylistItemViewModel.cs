﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LemonApp.Common.Funcs;
using LemonApp.MusicLib.Abstraction.Entities;
using LemonApp.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace LemonApp.ViewModels;

public class PlaylistItem(Playlist item, BitmapImage? cover)
{
    public Playlist ListInfo { get; set; } = item;
    public BitmapImage? Cover { get; set; } = cover;
}
public partial class PlaylistItemViewModel(
    MainNavigationService mainNavigationService) :ObservableObject
{
    [RelayCommand]
    private void Select(PlaylistItem value)
    {
        mainNavigationService.RequstNavigation(PageType.PlaylistPage, value.ListInfo);
    }

    public ObservableCollection<PlaylistItem> Playlists { get; set; } = [];
    public async Task SetPlaylistItems(IEnumerable<Playlist> list)
    {
        //compare
        if (Playlists.Count > 0)
        {
            static string selector(Playlist i) => i.Id + i.Name + i.Description;
            HashSet<string> now = Playlists.Select(i => selector(i.ListInfo)).ToHashSet();
            HashSet<string> reload = list.Select(selector).ToHashSet();
            if (now.SetEquals(reload)) return;
        }
        Playlists.Clear();
        try
        {
            foreach (var item in list)
            {
                var cover = await ImageCacheService.FetchData(item.Photo);
                Playlists.Add(new(item, cover));
            }
        }
        catch { }
    }
}
