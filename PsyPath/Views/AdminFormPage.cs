using PsyPath.UI;

namespace PsyPath.Views;

// экран визуального редактора: собирается заново при каждом показе, помнит поля с ошибками
public abstract class AdminFormPage : AdminBase
{
    readonly HashSet<object> invalid = new();

    protected bool HasInvalid => invalid.Count > 0;

    // для поля с проверкой: отмечает, принято ли его текущее значение
    protected Action<bool> Track()
    {
        var key = new object();
        return ok =>
        {
            if (ok) invalid.Remove(key);
            else invalid.Add(key);
        };
    }

    protected override void Refresh() => Rebuild();

    protected void Rebuild()
    {
        invalid.Clear();
        Build();
    }

    protected abstract void Build();

    protected async Task<bool> ConfirmLeaveWithErrors() =>
        !HasInvalid || await Dlg.Confirm(this, "ЕСТЬ ОШИБКИ", "Значения в полях, подсвеченных красным, не сохранятся. Выйти?", "ВЫЙТИ", "ОСТАТЬСЯ");
}
