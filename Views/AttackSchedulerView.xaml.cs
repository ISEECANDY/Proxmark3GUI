using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Proxmark3GUI.Views;

public partial class AttackSchedulerView : UserControl
{
    public ObservableCollection<AttackJobEntry> JobQueue { get; } = new();
    private int _jobCounter;
    private bool _isRunning;

    public AttackSchedulerView()
    {
        InitializeComponent();
        LvJobQueue.ItemsSource = JobQueue;
        UpdateSummary();
    }

    private void CmbRepeatMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CmbRepeatMode == null || TxtRepeatCount == null)
            return;

        var mode = (CmbRepeatMode.SelectedItem as ComboBoxItem)?.Content as string ?? "Run Once";
        TxtRepeatCount.IsEnabled = mode == "Repeat N Times";
    }

    private void BtnBrowseDict_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Dictionary files (*.dic;*.txt)|*.dic;*.txt|All files (*.*)|*.*",
            Title = "Select Dictionary File"
        };
        if (dialog.ShowDialog() == true)
        {
            TxtDictPath.Text = dialog.FileName;
        }
    }

    private void BtnAddJob_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(TxtJobName.Text) ? $"Job {_jobCounter + 1}" : TxtJobName.Text;
        var attackType = (CmbAttackType.SelectedItem as ComboBoxItem)?.Content as string ?? "Hardnested";
        var keyType = (CmbJobKeyType.SelectedItem as ComboBoxItem)?.Content as string ?? "Key A";
        var repeatMode = (CmbRepeatMode.SelectedItem as ComboBoxItem)?.Content as string ?? "Run Once";

        _jobCounter++;
        var job = new AttackJobEntry
        {
            Index = _jobCounter,
            Name = name,
            AttackType = attackType,
            KeyType = keyType,
            RepeatCount = repeatMode,
            Status = "Pending",
            DictFile = TxtDictPath.Text
        };

        JobQueue.Add(job);
        TxtJobName.Text = "";
        TxtStatus.Text = $"Job '{name}' added to queue.";
        UpdateSummary();
    }

    private void BtnStartAll_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;
        _isRunning = true;
        BtnStartAll.IsEnabled = false;
        TxtStatus.Text = "Starting all queued jobs...";

        foreach (var job in JobQueue)
        {
            if (job.Status == "Pending")
            {
                job.Status = "Running";
            }
        }
        UpdateSummary();
    }

    private void BtnStopAll_Click(object sender, RoutedEventArgs e)
    {
        _isRunning = false;
        BtnStartAll.IsEnabled = true;

        foreach (var job in JobQueue)
        {
            if (job.Status == "Running")
            {
                job.Status = "Pending";
            }
        }
        UpdateSummary();
        TxtStatus.Text = "All jobs stopped.";
    }

    private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (LvJobQueue.SelectedItem is AttackJobEntry job)
        {
            JobQueue.Remove(job);
            UpdateSummary();
            TxtStatus.Text = $"Job '{job.Name}' removed.";
        }
    }

    private void BtnClearQueue_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Clear all jobs from the queue?", "Clear Queue",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            JobQueue.Clear();
            _jobCounter = 0;
            UpdateSummary();
            TxtStatus.Text = "Queue cleared.";
        }
    }

    private void LvJobQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || LvJobQueue == null)
            return;

        if (LvJobQueue.SelectedItem is AttackJobEntry job)
        {
            TxtDetailName.Text = job.Name;
            TxtDetailAttack.Text = job.AttackType;
            TxtDetailKeyType.Text = job.KeyType;
            TxtDetailRepeat.Text = job.RepeatCount;
            TxtDetailStatus.Text = job.Status;
            TxtDetailKeys.Text = job.FoundKeys;
        }
    }

    private void UpdateSummary()
    {
        TxtTotalJobs.Text = JobQueue.Count.ToString();
        TxtCompletedJobs.Text = JobQueue.Count(j => j.Status == "Done").ToString();
        TxtRunningJobs.Text = JobQueue.Count(j => j.Status == "Running").ToString();
        TxtTotalKeys.Text = JobQueue.Count(j => j.Status == "Done" && j.FoundKeys.Length > 0).ToString();

        int completed = JobQueue.Count(j => j.Status is "Done" or "Failed");
        TxtProgress.Text = $"Completed: {completed} / Total: {JobQueue.Count}";
    }
}

public class AttackJobEntry
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string AttackType { get; set; } = "";
    public string KeyType { get; set; } = "";
    public string RepeatCount { get; set; } = "Run Once";
    public string Status { get; set; } = "Pending";
    public string DictFile { get; set; } = "";
    public string FoundKeys { get; set; } = "";
}
