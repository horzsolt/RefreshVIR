namespace RefreshVIR
{
    internal static class ApplicationBrand
    {
        private static Icon? _icon;

        internal static void Initialize()
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "gw.ico");
            if (File.Exists(iconPath))
            {
                _icon = new Icon(iconPath);
                return;
            }

            _icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }

        internal static void Apply(Form form)
        {
            if (_icon != null)
                form.Icon = (Icon)_icon.Clone();
        }
    }
}
