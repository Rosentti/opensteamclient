using Avalonia.Controls;
using ClientUI.Extensions;

namespace ClientUI.Views;

public partial class SecondFactorNeededDialog : Window
{
    public SecondFactorNeededDialog()
    {
        InitializeComponent();
        this.TranslatableInit();
    }
}