using ReactiveUI;

namespace Minark.Client.ViewModels;

/// <summary>
///     Base commune à tous les ViewModels.
///     ReactiveObject implémente INotifyPropertyChanged via RaiseAndSetIfChanged.
/// </summary>
public abstract class ViewModelBase : ReactiveObject;