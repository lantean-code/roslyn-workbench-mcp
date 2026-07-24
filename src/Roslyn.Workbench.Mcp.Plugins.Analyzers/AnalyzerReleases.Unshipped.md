## Unshipped

### New Rules

| Rule ID | Category | Severity | Notes |
| --- | --- | --- | --- |
| RWMCP001 | RoslynWorkbench.PluginAuthoring | Error | Do not mutate the Roslyn Workspace directly. |
| RWMCP002 | RoslynWorkbench.PluginAuthoring | Error | Use the invocation solution snapshot. |
| RWMCP003 | RoslynWorkbench.PluginAuthoring | Error | Plugin configuration must complete synchronously. |
| RWMCP004 | RoslynWorkbench.PluginAuthoring | Error | Do not retain startup configuration objects. |
| RWMCP005 | RoslynWorkbench.PluginAuthoring | Error | Implement exactly one handler contract. |
| RWMCP006 | RoslynWorkbench.PluginAuthoring | Error | Plugin handlers must not own a disposable lifetime. |
| RWMCP007 | RoslynWorkbench.PluginAuthoring | Error | Plugin handlers must not declare MEF imports. |
| RWMCP008 | RoslynWorkbench.PluginAuthoring | Error | External transport contract types must be public. |
| RWMCP009 | RoslynWorkbench.PluginAuthoring | Warning | Handler instance state requires thread-safety review. |
| RWMCP010 | RoslynWorkbench.PluginAuthoring | Warning | Avoid mutable static handler state. |
| RWMCP011 | RoslynWorkbench.PluginAuthoring | Warning | Handler field may own a disposable resource. |
| RWMCP012 | RoslynWorkbench.PluginAuthoring | Error | Query tools cannot declare destructive behaviour. |
| RWMCP013 | RoslynWorkbench.PluginAuthoring | Info | Observe the invocation cancellation token. |
| RWMCP014 | RoslynWorkbench.PluginAuthoring | Warning | Bound agent-facing query collections. |
| RWMCP015 | RoslynWorkbench.PluginAuthoring | Error | Plugin entry-point marker and contract must agree. |
| RWMCP016 | RoslynWorkbench.PluginAuthoring | Error | A plugin assembly cannot declare multiple marked entry points. |
| RWMCP017 | RoslynWorkbench.PluginAuthoring | Error | Declare the supported plugin API version. |
| RWMCP018 | RoslynWorkbench.PluginAuthoring | Error | Plugin identity metadata must not be blank. |
| RWMCP019 | RoslynWorkbench.PluginAuthoring | Error | Tool metadata must decorate a handler. |
