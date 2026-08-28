using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKBOT.Application.Queries;
using KKBOT.Contracts.Dashboard;
using KKBOT.Contracts.Instances;
using KKBOT.Contracts.Jobs;
using MediatR;

namespace KKBOT.ControlCenter.Wpf.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    public ObservableCollection<InstanceSummaryDto> Instances { get; } = new();
    public ObservableCollection<JobSummaryDto> Jobs { get; } = new();
    public ObservableCollection<DashboardSummaryDto> Summary { get; } = new();

    public DashboardViewModel()
    {
        // Resolve IMediator via DI or fallback static service locator (placeholder)
        _mediator = (IMediator)App.Current?.Resources["Mediator"]!;
        ExitCommand = new RelayCommand(ExitApplication);
        AboutCommand = new RelayCommand(ShowAbout);
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        var instanceResult = await _mediator.Send(new GetInstancesQuery());
        Instances.Clear();
        foreach (var item in instanceResult.Items)
            Instances.Add(item);

        var jobResult = await _mediator.Send(new GetJobsQuery());
        Jobs.Clear();
        foreach (var job in jobResult.Items)
            Jobs.Add(job);

        var summary = await _mediator.Send(new GetDashboardSummaryQuery());
        Summary.Clear();
        Summary.Add(summary);
    }

    private void ExitApplication()
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void ShowAbout()
    {
        System.Windows.MessageBox.Show("KKBOT Control Center\nVersion 1.0.0", "About", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public ICommand ExitCommand { get; }
    public ICommand AboutCommand { get; }
}