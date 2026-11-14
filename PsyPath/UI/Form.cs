namespace PsyPath.UI;

// поля визуального редактора: подпись сверху, значение сразу пишется в модель
public static class Form
{
    public static View Section(string title)
    {
        var t = Ui.Text(title, 13, Pal.RedInk, true);
        t.Margin = new Thickness(0, 10, 0, 0);
        return t;
    }

    public static View Hint(string text) => Ui.Text(text, 10, Pal.Ink2);

    public static View Field(string label, string? value, Action<string> set, bool multiline = false, string? hint = null)
    {
        View input;
        if (multiline)
        {
            var editor = new Editor
            {
                Text = value,
                FontFamily = "Body",
                FontSize = 14 * Ui.Scale,
                TextColor = Pal.Ink,
                BackgroundColor = Colors.Transparent,
                AutoSize = EditorAutoSizeOption.TextChanges
            };
            editor.TextChanged += (s, e) => set(e.NewTextValue ?? "");
            input = editor;
        }
        else
        {
            var entry = new Entry
            {
                Text = value,
                FontFamily = "Body",
                FontSize = 14 * Ui.Scale,
                TextColor = Pal.Ink,
                BackgroundColor = Colors.Transparent
            };
            entry.TextChanged += (s, e) => set(e.NewTextValue ?? "");
            input = entry;
        }
        return Labeled(label, Ui.Card(input, Pal.Paper2, 4), hint, null);
    }

    // список строк: одна строка — один пункт
    public static View Lines(string label, List<string> list, string? hint = null) =>
        Field(label, string.Join("\n", list), text =>
        {
            list.Clear();
            list.AddRange(text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0));
        }, multiline: true, hint: hint ?? "Каждый пункт с новой строки.");

    // целое число; неверный ввод подсвечивается и в модель не попадает
    public static View Int(string label, int value, Action<int> set, Action<bool> valid, string? hint = null) =>
        Parsed(label, value.ToString(), text =>
        {
            bool ok = int.TryParse(text.Trim(), out int n) && n >= 0;
            if (ok) set(n);
            return ok ? null : "Нужно целое число от 0";
        }, valid, hint, Keyboard.Numeric);

    // номера через запятую или пробел: «1, 5, 9»
    public static View Numbers(string label, List<int> list, Action<bool> valid, string? hint = null) =>
        Parsed(label, string.Join(", ", list), text =>
        {
            var parts = text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var nums = new List<int>();
            foreach (var p in parts)
            {
                if (!int.TryParse(p, out int n) || n < 1) return "Только номера вопросов через запятую";
                nums.Add(n);
            }
            list.Clear();
            list.AddRange(nums.Distinct().OrderBy(n => n));
            return null;
        }, valid, hint, Keyboard.Text);

    // apply возвращает текст ошибки или null, если значение принято
    static View Parsed(string label, string text, Func<string, string?> apply, Action<bool> valid, string? hint, Keyboard keyboard)
    {
        var error = Ui.Text("", 10, Pal.RedInk, true);
        var entry = new Entry
        {
            Text = text,
            FontFamily = "Body",
            FontSize = 14 * Ui.Scale,
            TextColor = Pal.Ink,
            BackgroundColor = Colors.Transparent,
            Keyboard = keyboard
        };
        entry.TextChanged += (s, e) =>
        {
            string? err = apply(e.NewTextValue ?? "");
            error.Text = err ?? "";
            valid(err == null);
        };
        return Labeled(label, Ui.Card(entry, Pal.Paper2, 4), hint, error);
    }

    // выбор одного варианта плашками в две колонки; подсвечена текущая
    public static View Choice(string label, IReadOnlyList<(string Value, string Caption)> options, string current, Action<string> set)
    {
        var grid = new Grid
        {
            ColumnSpacing = 6,
            RowSpacing = 6,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        var cards = new List<(Border Card, string Value)>();
        void Paint(string selected)
        {
            foreach (var c in cards)
                c.Card.BackgroundColor = c.Value == selected ? Pal.Amber : Pal.Paper;
        }

        for (int i = 0; i < options.Count; i++)
        {
            if (i % 2 == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var (value, caption) = options[i];
            var card = Ui.Card(Ui.Text(caption, 12, Pal.Ink, true, TextAlignment.Center), Pal.Paper, 8);
            Grid.SetRow(card, i / 2);
            Grid.SetColumn(card, i % 2);
            Ui.OnTap(card, () =>
            {
                set(value);
                Paint(value);
            });
            cards.Add((card, value));
            grid.Children.Add(card);
        }
        Paint(current);
        return Labeled(label, grid, null, null);
    }

    // ползунок 0–100 с процентами справа; set получает целое значение при каждом сдвиге
    public static View Percent(string label, int current, Action<int> set)
    {
        var value = Ui.Text($"{current}%", 12, Pal.Ink, true, TextAlignment.End);
        value.VerticalTextAlignment = TextAlignment.Center;
        value.WidthRequest = 48;
        var slider = new Slider(0, 100, current)
        {
            MinimumTrackColor = Pal.Violet,
            MaximumTrackColor = Pal.Line,
            ThumbColor = Pal.Violet,
            MinimumHeightRequest = 48
        };
        int last = current;
        slider.ValueChanged += (s, e) =>
        {
            int n = (int)Math.Round(e.NewValue);
            if (n == last) return;
            last = n;
            value.Text = $"{n}%";
            set(n);
        };
        var row = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        row.Children.Add(slider);
        Grid.SetColumn(value, 1);
        row.Children.Add(value);
        return Labeled(label, row, null, null);
    }

    static View Labeled(string label, View input, string? hint, View? error)
    {
        var box = new VerticalStackLayout { Spacing = 3 };
        box.Children.Add(Ui.Text(label, 11, Pal.Ink2, true));
        box.Children.Add(input);
        if (error != null) box.Children.Add(error);
        if (hint != null) box.Children.Add(Hint(hint));
        return box;
    }

    // текстовая кнопка: «✕ удалить», «▲», «▼», «ВЫЙТИ». Зона нажатия не меньше 48×48 dp (Android),
    // даже если сама надпись маленькая
    public static View Link(string text, Color color, Action action, TextAlignment align = TextAlignment.Start)
    {
        var t = Ui.Text(text, 13, color, true, align);
        t.VerticalTextAlignment = TextAlignment.Center;
        t.MinimumHeightRequest = 48;
        t.MinimumWidthRequest = 48;
        t.Padding = new Thickness(8, 0);
        Ui.OnTap(t, action);
        return t;
    }
}
