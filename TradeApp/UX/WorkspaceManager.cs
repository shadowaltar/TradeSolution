using Autofac;
using DevExpress.Xpf.Docking;
using System;
using System.Collections.Generic;
using System.Windows;
using TradeApp.Utils;
using TradeApp.ViewModels;
using DockLayoutManager = DevExpress.Xpf.Docking.DockLayoutManager;

namespace TradeApp.UX
{
    public class WorkspaceManager
    {
        private readonly Dictionary<PresetPanelType, string> _presetPanelNames = new();
        private Lazy<Workspace?> LazyLastWorkspace;

        private DockLayoutManager _dockLayoutManager;
        private IDockController _dockController;
        private IContainer _container;
        public Workspace? LastWorkspace => LazyLastWorkspace.Value;
        public bool ShallLoadLastWorkspaceOnInit { get; }
        public Dictionary<string, AbstractViewModel> PanelViewModels { get; } = new();

        public WorkspaceManager()
        {
            LazyLastWorkspace = new Lazy<Workspace?>(LoadLastWorkspace);
            ShallLoadLastWorkspaceOnInit = false; // TODO
        }

        public void Initialize(DockLayoutManager dockLayoutManager, IContainer container)
        {
            _container = container;
            _dockLayoutManager = dockLayoutManager;
            _dockController = dockLayoutManager.DockController;
        }

        private Workspace? LoadLastWorkspace()
        {
            return new Workspace();
        }

        public void LoadPanel(PresetPanelType type)
        {
            var attr = type.GetAttribute<ViewResourceAttribute>();
            if (attr == null)
                return;

            Close(PresetPanelType.Welcome);

            var path = "/TradeApp;component/" + attr.ResourcePath;
            BaseLayoutItem? view = null;

            if (Application.LoadComponent(new Uri(path, UriKind.Relative)) is LayoutPanel panel)
            {
                _dockController.Dock(panel);
                view = panel;
            }
            else if (Application.LoadComponent(new Uri(path, UriKind.Relative)) is LayoutGroup group)
            {
                _dockController.Dock(group);
                view = group;
            }

            if (view?.Name == null)
            {
                throw new InvalidOperationException("Must add x:Name to preset layout panels/groups!");
            }

            if (view != null)
            {
                _presetPanelNames[type] = view.Name;
                if (attr.ViewModelType != null)
                {
                    var viewModel = _container.Resolve(attr.ViewModelType);
                    view.DataContext = viewModel;
                    if (viewModel is IViewAwared viewAwared)
                    {
                        viewAwared.SetView(view);
                    }

                    PanelViewModels[Guid.NewGuid().ToString()] = (AbstractViewModel)viewModel;
                }
            }
        }

        private void Close(PresetPanelType type)
        {
            foreach (var item in _dockLayoutManager.GetItems())
            {
                if (item.Name == (_presetPanelNames.TryGetValue(type, out var name) ? name : ""))
                {
                    _dockController.Close(item);
                }
            }
        }
    }
}
