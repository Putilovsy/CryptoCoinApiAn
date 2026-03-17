using System.Windows.Input;

namespace CryptoMonitor.Helpers
{
    public class RelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;

        public RelayCommand(Func<object, Task> execute)
        {
            _execute = execute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public async void Execute(object parameter)
        {
            await _execute(parameter);
        }
    }
}