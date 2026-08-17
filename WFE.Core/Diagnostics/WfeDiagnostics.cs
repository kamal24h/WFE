
// WFE.Core/Diagnostics/WfeDiagnostics.cs
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WFE.Core.Diagnostics;

public static class WfeDiagnostics
{

    // todo: فعلاً بخاطر اهمیت زمان اجرای گردش کار، فرایندهای خطایابی و تشخیصی را اجرا نمیکنم.
    public const string ServiceName = "WFE.Engine";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    // شمارنده‌ها و سنجه‌های عملکردی
    public static readonly Counter<long> TransitionsCounter =
        Meter.CreateCounter<long>("wfe.transitions.count", description: "تعداد کل ترنزیشن‌های انجام‌شده");

    public static readonly Histogram<double> TransitionDuration =
        Meter.CreateHistogram<double>("wfe.transition.duration.ms", unit: "ms", description: "مدت زمان اجرای هر ترنزیشن");
}