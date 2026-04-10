using DbForge.WPF.ViewModels.Base;
using System.Collections.ObjectModel;

namespace DbForge.WPF.ViewModels.Compare
{
    public class DiffTreeNode : BaseViewModel
    {
        public string Label { get; set; } = string.Empty;

        /// <summary>Icon glyph (Segoe MDL2 or emoji fallback).</summary>
        public string Icon { get; set; } = "📄";

        /// <summary>
        /// "Table" | "Columns" | "Column" | "Indexes" | "Index"
        /// | "ForeignKeys" | "ForeignKey" | "PrimaryKey"
        /// Drives colour in XAML DataTriggers.
        /// </summary>
        public string NodeKind { get; set; } = "Table";

        /// <summary>
        /// "Added" | "Removed" | "Modified" | "Identical"
        /// Drives the coloured status badge.
        /// </summary>
        public string DiffStatus { get; set; } = "Identical";

        public ObservableCollection<DiffTreeNode> Children { get; } = new();

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => Set(ref _isExpanded, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        // ── Status badge colour (bound in XAML) ──────────────────────────────────
        public string StatusColour => DiffStatus switch
        {
            "Added" => "#22C55E",
            "Removed" => "#EF4444",
            "Modified" => "#3B82F6",
            _ => "#4B5563"
        };
    }
}
