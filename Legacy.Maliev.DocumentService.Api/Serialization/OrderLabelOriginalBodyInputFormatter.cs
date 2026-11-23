using System.Buffers;
using System.Text;
using Legacy.Maliev.DocumentService.Api.Controllers;
using Legacy.Maliev.DocumentService.Domain.OrderLabel;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Legacy.Maliev.DocumentService.Api.Serialization;

/// <summary>Adds original OrderLabel body admission for the OrderLabel request.</summary>
/// <param name="loggers">The application logger factory.</param>
/// <param name="pools">The application object pool provider.</param>
public sealed class OrderLabelOriginalBodyMvcOptionsSetup(ILoggerFactory loggers, ObjectPoolProvider pools) : IConfigureOptions<MvcOptions>
{
    /// <inheritdoc />
    public void Configure(MvcOptions options)
    {
        if (options.InputFormatters.OfType<OrderLabelOriginalBodyInputFormatter>().Any()) return;
        var source = new MvcNewtonsoftJsonOptions();
        source.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        source.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        // Keep stock MVC defaults local to the input reader. The existing STJ
        // formatter, output, DTO validation and documentation remain registered.
        var logger = new SourceBodyLogger(loggers.CreateLogger(
            "Microsoft.AspNetCore.Mvc.Formatters.NewtonsoftJsonInputFormatter"));
        options.InputFormatters.Insert(0, new OrderLabelOriginalBodyInputFormatter(
            new BufferedSourceFormatter(logger, source, pools, options)));
    }

    private sealed class SourceBodyLogger(ILogger target) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            target.BeginScope("OrderLabel source body admission");
        public bool IsEnabled(LogLevel logLevel) => target.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (target.IsEnabled(logLevel))
                target.Log(logLevel, eventId, "SourceBodyAdmissionFailure ExceptionType={ExceptionType}",
                    exception?.GetType().Name ?? "InputFormatter");
        }
    }

    private sealed class BufferedSourceFormatter(
        ILogger logger, MvcNewtonsoftJsonOptions json, ObjectPoolProvider pools, MvcOptions mvc)
        : NewtonsoftJsonInputFormatter(logger, json.SerializerSettings, ArrayPool<char>.Shared, pools, mvc, json)
    {
        private const long DefaultRequestLimit = 30_000_000;
        private readonly int memoryThreshold = json.InputFormatterMemoryBufferThreshold;

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
        {
            // TextInputFormatter selected the supported encoding before this method.
            // Drain asynchronously before the stock reader performs synchronous I/O.
            var request = context.HttpContext.Request;
            var original = request.Body;
            var originalPosition = original.CanSeek ? original.Position : (long?)null;
            var limit = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize ?? DefaultRequestLimit;
            await using var buffered = new FileBufferingReadStream(original, memoryThreshold, limit,
                static () => Environment.GetEnvironmentVariable("ASPNETCORE_TEMP") ?? Path.GetTempPath());
            try
            {
                request.Body = buffered;
                await buffered.DrainAsync(context.HttpContext.RequestAborted);
                buffered.Position = 0;
                return await base.ReadRequestBodyAsync(context, encoding);
            }
            finally
            {
                request.Body = original;
                if (originalPosition.HasValue) original.Position = originalPosition.Value;
            }
        }
    }
}

internal sealed class OrderLabelOriginalBodyInputFormatter(NewtonsoftJsonInputFormatter source)
    : IInputFormatter, IInputFormatterExceptionPolicy
{
    // Subclassing the inner reader must not widen MVC's selected exception policy.
    public InputFormatterExceptionPolicy ExceptionPolicy => InputFormatterExceptionPolicy.MalformedInputExceptions;

    public bool CanRead(InputFormatterContext context) =>
        IsOwned(context) && source.CanRead(context);

    public async Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
    {
        if (!IsOwned(context))
            throw new InvalidOperationException("The OrderLabel source body formatter received an unowned contract.");

        // This metadata describes only the inert source object. The projected
        // request still undergoes the unchanged controller's normal MVC validation.
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(OriginalOrderLabel));
        var state = new ModelStateDictionary(context.ModelState.MaxAllowedErrors);
        var originalContext = new InputFormatterContext(context.HttpContext, context.ModelName, state, metadata,
            context.ReaderFactory, context.TreatEmptyInputAsDefaultValue);
        InputFormatterResult result;
        try
        {
            result = await source.ReadAsync(originalContext);
        }
        catch (InvalidOperationException exception) when (
            exception.Source == "Newtonsoft.Json" &&
            exception.Message == "Current error context error is different to requested error.")
        {
            // Newtonsoft can throw while recovering from its JSON depth error.
            result = InputFormatterResult.Failure();
        }
        if (result.HasError)
        {
            // Do not reflect an input property, value or caller-controlled prefix
            // into model errors. Preserve unsupported-charset classification.
            var key = context.ModelName is "" or "item" or "request" ? context.ModelName : "$";
            if (state.Values.SelectMany(value => value.Errors).Any(error => error.Exception is UnsupportedContentTypeException))
                context.ModelState.TryAddModelError(key,
                    new UnsupportedContentTypeException("The request content type is unsupported."), context.Metadata);
            else
                context.ModelState.TryAddModelError(key, "The request body contains an invalid value.");
            return InputFormatterResult.Failure();
        }
        if (!result.IsModelSet) return result;
        if (result.Model is null) return InputFormatterResult.Success(null);
        var value = (OriginalOrderLabel)result.Model;
        return InputFormatterResult.Success(new OrderLabel
        {
            Id = value.Id!,
            Name = value.Name!,
            OrderQuantity = value.OrderQuantity,
            ManufactureQuantity = value.ManufactureQuantity,
            RemainingQuantity = value.RemainingQuantity,
            Process = value.Process!,
            Material = value.Material!,
            Color = value.Color!,
            SurfaceFinish = value.SurfaceFinish!,
            Description = value.Description!,
        });
    }

    private static bool IsOwned(InputFormatterContext context)
    {
        var action = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        return context.ModelType == typeof(OrderLabel)
            && action is not null
            && action.ControllerTypeInfo.AsType() == typeof(PdfsController)
            && action.MethodInfo == typeof(PdfsController).GetMethod(nameof(PdfsController.CreateOrderLabelAsync));
    }

    // Complete inert source shape: seven strings and three Int32 quantities.
    // Nullable annotations here do not add or remove MVC validation on OrderLabel.
    private sealed class OriginalOrderLabel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int OrderQuantity { get; set; }
        public int ManufactureQuantity { get; set; }
        public int RemainingQuantity { get; set; }
        public string? Process { get; set; }
        public string? Material { get; set; }
        public string? Color { get; set; }
        public string? SurfaceFinish { get; set; }
        public string? Description { get; set; }
    }
}
