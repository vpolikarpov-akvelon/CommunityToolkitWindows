// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Windows.UI;

#if WINAPPSDK
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
#else
using Windows.Foundation.Metadata;
using Windows.UI.Composition;
using Windows.UI.Xaml.Hosting;
#endif

namespace CommunityToolkit.WinUI;

/// <summary>
/// The base class for attached shadows. (Not supported in Uno.)
/// </summary>
public abstract partial class AttachedShadowBase : DependencyObject, IAttachedShadow
{
#if !WINAPPSDK
    // TODO: If we ever bump UWP min-version, remove this, as available after 1903.
    /// <summary>
    /// Gets a value indicating whether or not Composition's VisualSurface is supported.
    /// </summary>
    protected static readonly bool SupportsCompositionVisualSurface = ApiInformation.IsTypePresent(typeof(CompositionVisualSurface).FullName);
#endif

    /// <summary>
    /// The <see cref="DependencyProperty"/> for <see cref="BlurRadius"/>.
    /// </summary>
    public static readonly DependencyProperty BlurRadiusProperty =
        DependencyProperty.Register(nameof(BlurRadius), typeof(double), typeof(AttachedShadowBase), new PropertyMetadata(12d, OnDependencyPropertyChanged));

    /// <summary>
    /// The <see cref="DependencyProperty"/> for <see cref="Color"/>.
    /// </summary>
    public static readonly DependencyProperty ColorProperty =
        DependencyProperty.Register(nameof(Color), typeof(Color), typeof(AttachedShadowBase), new PropertyMetadata(Colors.Black, OnDependencyPropertyChanged));

    /// <summary>
    /// The <see cref="DependencyProperty"/> for <see cref="Opacity"/>.
    /// </summary>
    public static readonly DependencyProperty OffsetProperty =
        DependencyProperty.Register(
            nameof(Offset),
            typeof(string), // Needs to be string as we can't convert in XAML natively from Vector3, see https://github.com/microsoft/microsoft-ui-xaml/issues/3896
            typeof(AttachedShadowBase),
            new PropertyMetadata(string.Empty, OnDependencyPropertyChanged));

    /// <summary>
    /// The <see cref="DependencyProperty"/> for <see cref="Opacity"/>
    /// </summary>
    public static readonly DependencyProperty OpacityProperty =
        DependencyProperty.Register(nameof(Opacity), typeof(double), typeof(AttachedShadowBase), new PropertyMetadata(1d, OnDependencyPropertyChanged));

#if !WINAPPSDK
    /// <summary>
    /// Gets a value indicating whether or not this <see cref="AttachedShadowBase"/> implementation is supported on the current platform.
    /// </summary>
    protected abstract bool IsSupported { get; }
#endif

    /// <summary>
    /// Gets or sets the collection of <see cref="AttachedShadowElementContext"/> for each element this <see cref="AttachedShadowBase"/> is connected to.
    /// </summary>
    private ConditionalWeakTable<FrameworkElement, AttachedShadowElementContext> ShadowElementContextTable { get; set; } = new();

    /// <inheritdoc/>
    public double BlurRadius
    {
        get => (double)GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    /// <inheritdoc/>
    public double Opacity
    {
        get => (double)GetValue(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    /// <inheritdoc/>
    public string Offset
    {
        get => (string)GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    /// <inheritdoc/>
    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether or not OnSizeChanged should be called when <see cref="FrameworkElement.SizeChanged"/> is fired.
    /// </summary>
    protected internal abstract bool SupportsOnSizeChangedEvent { get; }

    /// <summary>
    /// Use this method as the <see cref="PropertyChangedCallback"/> for <see cref="DependencyProperty">DependencyProperties</see> in derived classes.
    /// </summary>
    protected static void OnDependencyPropertyChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        (sender as AttachedShadowBase)?.CallPropertyChangedForEachElement(args.Property, args.OldValue, args.NewValue);
    }

    internal void ConnectElement(FrameworkElement element)
    {
#if !WINAPPSDK
        if (!IsSupported)
        {
        	return;
        }
#endif

        if (ShadowElementContextTable.TryGetValue(element, out var context))
        {
            return;
        }

        context = new AttachedShadowElementContext(this, element);
        ShadowElementContextTable.Add(element, context);
    }

    internal void DisconnectElement(FrameworkElement element)
    {
        if (ShadowElementContextTable.TryGetValue(element, out var context))
        {
            context.DisconnectFromElement();
            ShadowElementContextTable.Remove(element);
        }
    }

    /// <summary>
    /// Override to handle when the <see cref="AttachedShadowElementContext"/> for an element is being initialized.
    /// </summary>
    /// <param name="context">The <see cref="AttachedShadowElementContext"/> that is being initialized.</param>
    protected internal virtual void OnElementContextInitialized(AttachedShadowElementContext context)
    {
        OnPropertyChanged(context, OpacityProperty, Opacity, Opacity);
        OnPropertyChanged(context, BlurRadiusProperty, BlurRadius, BlurRadius);
        OnPropertyChanged(context, ColorProperty, Color, Color);
        OnPropertyChanged(context, OffsetProperty, Offset, Offset);
        UpdateShadowClip(context);
        UpdateShadowMask(context);
        SetElementChildVisual(context);
    }

    /// <summary>
    /// Override to handle when the <see cref="AttachedShadowElementContext"/> for an element is being uninitialized.
    /// </summary>
    /// <param name="context">The <see cref="AttachedShadowElementContext"/> that is being uninitialized.</param>
    protected internal virtual void OnElementContextUninitialized(AttachedShadowElementContext context)
    {
        context.ClearAndDisposeResources();
        ElementCompositionPreview.SetElementChildVisual(context.Element, null!);
    }

    /// <inheritdoc/>
    public AttachedShadowElementContext? GetElementContext(FrameworkElement element)
    {
        if (ShadowElementContextTable.TryGetValue(element, out var context))
        {
            return context;
        }

        return null;
    }

    /// <inheritdoc/>
    public IEnumerable<AttachedShadowElementContext> EnumerateElementContexts()
    {
#if !NETSTANDARD2_0
        foreach (var kvp in ShadowElementContextTable)
        {
            yield return kvp.Value;
        }
#else
        // TODO: If Uno supports composition, we also need a ConditionalWeakTable polyfill...
        return Enumerable.Empty<AttachedShadowElementContext>();
#endif
    }

    /// <summary>
    /// Sets <see cref="AttachedShadowElementContext.SpriteVisual"/> as a child visual on <see cref="AttachedShadowElementContext.Element"/>
    /// </summary>
    /// <param name="context">The <see cref="AttachedShadowElementContext"/> this operation will be performed on.</param>
    protected virtual void SetElementChildVisual(AttachedShadowElementContext context)
    {
        ElementCompositionPreview.SetElementChildVisual(context.Element, context.SpriteVisual!);
    }

    private void CallPropertyChangedForEachElement(DependencyProperty property, object oldValue, object newValue)
    {
#if !NETSTANDARD2_0
        foreach (var context in ShadowElementContextTable)
        {
            if (context.Value.IsInitialized)
            {
                OnPropertyChanged(context.Value, property, oldValue, newValue);
            }
        }
#endif
    }

    /// <summary>
    /// Get a <see cref="CompositionBrush"/> in the shape of the element that is casting the shadow.
    /// </summary>
    /// <returns>A <see cref="CompositionBrush"/> representing the shape of an element.</returns>
    protected virtual CompositionBrush? GetShadowMask(AttachedShadowElementContext context)
    {
        return null;
    }

    /// <summary>
    /// Get the <see cref="CompositionClip"/> for the shadow's <see cref="SpriteVisual"/>
    /// </summary>
    /// <returns>A <see cref="CompositionClip"/> for the extent of the shadowed area.</returns>
    protected virtual CompositionClip? GetShadowClip(AttachedShadowElementContext context)
    {
        return null;
    }

    /// <summary>
    /// Update the mask that gives the shadow its shape.
    /// </summary>
    protected void UpdateShadowMask(AttachedShadowElementContext context)
    {
        if (!context.IsInitialized || context.Shadow == null)
        {
            return;
        }

        context.Shadow.Mask = GetShadowMask(context);
    }

    /// <summary>
    /// Update the clipping on the shadow's <see cref="SpriteVisual"/>.
    /// </summary>
    protected void UpdateShadowClip(AttachedShadowElementContext context)
    {
        if (!context.IsInitialized || context.SpriteVisual == null)
        {
            return;
        }

        context.SpriteVisual.Clip = GetShadowClip(context);
    }

    /// <summary>
    /// This method is called when a DependencyProperty is changed.
    /// </summary>
    protected virtual void OnPropertyChanged(AttachedShadowElementContext context, DependencyProperty property, object oldValue, object newValue)
    {
        if (!context.IsInitialized || context.Shadow == null)
        {
            return;
        }

        if (property == BlurRadiusProperty)
        {
            context.Shadow.BlurRadius = (float)(double)newValue;
        }
        else if (property == OpacityProperty)
        {
            context.Shadow.Opacity = (float)(double)newValue;
        }
        else if (property == ColorProperty && newValue is Color color)
        {
            context.Shadow.Color = color;
        }
        else if (property == OffsetProperty && newValue is string value)
        {
            context.Shadow.Offset = value.ToVector3();
        }
    }

    /// <summary>
    /// This method is called when the element size changes, and <see cref="SupportsOnSizeChangedEvent"/> = <see cref="bool">true</see>.
    /// </summary>
    /// <param name="context">The <see cref="AttachedShadowElementContext"/> for the <see cref="FrameworkElement"/> firing its SizeChanged event</param>
    /// <param name="newSize">The new size of the <see cref="FrameworkElement"/></param>
    /// <param name="previousSize">The previous size of the <see cref="FrameworkElement"/></param>
    protected internal virtual void OnSizeChanged(AttachedShadowElementContext context, Size newSize, Size previousSize)
    {
    }
}
