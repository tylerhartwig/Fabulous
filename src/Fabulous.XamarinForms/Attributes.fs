namespace Fabulous.XamarinForms

open System.Runtime.CompilerServices
open Fabulous
open Fabulous.ScalarAttributeDefinitions
open Xamarin.Forms
open System

[<Struct>]
type AppThemeValues<'T> = { Light: 'T; Dark: 'T voption }

module AppTheme =
    let inline create<'T> (light: 'T) (dark: 'T option) =
        { Light = light
          Dark =
              match dark with
              | None -> ValueNone
              | Some v -> ValueSome v }

[<Struct>]
type ValueEventData<'data, 'eventArgs> =
    { Value: 'data
      Event: 'eventArgs -> obj }

module ValueEventData =
    let create (value: 'data) (event: 'eventArgs -> obj) = { Value = value; Event = event }

/// Xamarin Forms specific attributes that can be encoded as 8 bytes
module SmallScalars =
    /// In reality XF takes 7 values of 4 bytes each.
    /// Which is larger than 8 bytes an unusual to represent a color
    /// So we are just going to bluntly convert it into 8 bytes in rgba form
    /// note that we allocate 2 bytes for each channel in RGBA format to improve precision
    /// That being said, it is a lossy conversion
    module Color =
        let inline encode (v: Color) : uint64 =
            let r = uint64(uint16(v.R * 65535.0)) <<< 48
            let g = uint64(uint16(v.G * 65535.0)) <<< 32
            let b = uint64(uint16(v.B * 65535.0)) <<< 16
            let a = uint64(uint16(v.A * 65535.0))

            r ||| g ||| b ||| a

        let inline decode (encoded: uint64) : Color =
            let r =
                (encoded &&& 0xFFFF000000000000UL) >>> 48

            let g =
                (encoded &&& 0x0000FFFF00000000UL) >>> 32

            let b =
                (encoded &&& 0x00000000FFFF0000UL) >>> 16

            let a = (encoded &&& 0x000000000000FFFFUL)
            let inline toFloat value = (float value) / 65535.0
            Color.FromRgba(toFloat r, toFloat g, toFloat b, toFloat a)

    module LayoutOptions =
        let inline encode (v: LayoutOptions) : uint64 =
            let alignment = uint64 v.Alignment

            let expands: uint64 = if v.Expands then 1UL else 0UL

            (alignment <<< 32) ||| expands

        let inline decode (encoded: uint64) : LayoutOptions =
            let alignment =
                enum<LayoutAlignment>(int((encoded &&& 0xFFFFFFFF00000000UL) >>> 32))

            let expands = (encoded &&& 0x00000000FFFFFFFFUL) = 1UL

            LayoutOptions(alignment, expands)

[<Extension>]
type SmallScalarExtensions() =
    [<Extension>]
    static member inline WithValue(this: SmallScalarAttributeDefinition<LayoutOptions>, value) =
        this.WithValue(value, SmallScalars.LayoutOptions.encode)

    [<Extension>]
    static member inline WithValue(this: SmallScalarAttributeDefinition<Color>, value) =
        this.WithValue(value, SmallScalars.Color.encode)

module Attributes =
    /// Define an attribute for a BindableProperty
    let inline defineBindable<'modelType, 'valueType>
        (bindableProperty: BindableProperty)
        ([<InlineIfLambda>] convertValue: 'modelType -> 'valueType)
        ([<InlineIfLambda>] compare: 'modelType -> 'modelType -> ScalarAttributeComparison)
        =
        Attributes.defineScalar<'modelType, 'valueType>
            bindableProperty.PropertyName
            convertValue
            compare
            (fun _ newValueOpt node ->
                let target = node.Target :?> BindableObject

                match newValueOpt with
                | ValueNone -> target.ClearValue(bindableProperty)
                | ValueSome v -> target.SetValue(bindableProperty, v))

    /// Define an attribute for a BindableProperty supporting equality comparison
    let inline defineBindableWithEquality<'T when 'T: equality> (bindableProperty: BindableProperty) =
        Attributes.defineSimpleScalarWithEquality<'T>
            bindableProperty.PropertyName
            (fun _ newValueOpt node ->
                let target = node.Target :?> BindableObject

                match newValueOpt with
                | ValueNone -> target.ClearValue(bindableProperty)
                | ValueSome v -> target.SetValue(bindableProperty, v))

    /// Define an attribute that can fit into 8 bytes encoded as uint64 (such as float or bool) for a BindableProperty
    let inline defineSmallBindable<'T> (bindableProperty: BindableProperty) ([<InlineIfLambda>] decode: uint64 -> 'T) =
        Attributes.defineSmallScalar<'T>
            bindableProperty.PropertyName
            decode
            (fun _ newValueOpt node ->
                let target = node.Target :?> BindableObject

                match newValueOpt with
                | ValueNone -> target.ClearValue(bindableProperty)
                | ValueSome v -> target.SetValue(bindableProperty, v))

    /// Define a float attribute for a BindableProperty and encode it as a small scalar (8 bytes)
    let inline defineBindableFloat (bindableProperty: BindableProperty) =
        defineSmallBindable bindableProperty SmallScalars.Float.decode

    /// Define a boolean attribute for a BindableProperty and encode it as a small scalar (8 bytes)
    let inline defineBindableBool (bindableProperty: BindableProperty) =
        defineSmallBindable bindableProperty SmallScalars.Bool.decode

    /// Define an int attribute for a BindableProperty and encode it as a small scalar (8 bytes)
    let inline defineBindableInt (bindableProperty: BindableProperty) =
        defineSmallBindable bindableProperty SmallScalars.Int.decode

    /// Define a Color attribute for a BindableProperty and encode it as a small scalar (8 bytes).
    /// Note that this uses a faster but a lossy internal representation
    /// that is, it allocates 2 bytes for each of channel of RGBA.
    /// Technically it might loose precision of (0.0 .. 1.0) float range used in XF.Color.
    /// If you want to avoid any potential loss of accuracy you can use "defineBindable" instead.
    /// It is 100% accurate but allocates XF.Color values on the heap (thus slower)
    let inline defineBindableColor (bindableProperty: BindableProperty) =
        defineSmallBindable bindableProperty SmallScalars.Color.decode

    /// Define an enum attribute for a BindableProperty and encode it as a small scalar (8 bytes)
    let inline defineBindableEnum< ^T when ^T: enum<int>>
        (bindableProperty: BindableProperty)
        : SmallScalarAttributeDefinition< ^T > =
        Attributes.defineEnum< ^T>
            bindableProperty.PropertyName
            (fun _ newValueOpt node ->
                let target = node.Target :?> BindableObject

                match newValueOpt with
                | ValueNone -> target.ClearValue(bindableProperty)
                | ValueSome v -> target.SetValue(bindableProperty, v))

    /// Define an attribute that supports values for both Light and Dark themes
    let inline defineBindableAppTheme<'T when 'T: equality> (bindableProperty: BindableProperty) =
        Attributes.defineSimpleScalarWithEquality<AppThemeValues<'T>>
            bindableProperty.PropertyName
            (fun _ newValueOpt node ->
                let target = node.Target :?> BindableObject

                match newValueOpt with
                | ValueNone -> target.ClearValue(bindableProperty)
                | ValueSome { Light = light; Dark = ValueNone } -> target.SetValue(bindableProperty, light)
                | ValueSome { Light = light; Dark = ValueSome dark } ->
                    target.SetOnAppTheme(bindableProperty, light, dark))
            
    /// Define an attribute storing a Widget for a bindable property
    let inline defineBindableWidget (bindableProperty: BindableProperty) =
        Attributes.definePropertyWidget
            bindableProperty.PropertyName
            (fun target ->
                let childTarget =
                    (target :?> BindableObject)
                        .GetValue(bindableProperty)

                ViewNode.get childTarget)
            (fun target value ->
                let bindableObject = target :?> BindableObject

                if value = null then
                    bindableObject.ClearValue(bindableProperty)
                else
                    bindableObject.SetValue(bindableProperty, value))

    /// Update both a property and its related event.
    /// This definition makes sure that the event is only raised when the property is changed by the user,
    /// and not when the property is set by the code
    let defineBindableWithEvent<'data, 'args>
        name
        (bindableProperty: BindableProperty)
        (getEvent: obj -> IEvent<EventHandler<'args>, 'args>)
        : SimpleScalarAttributeDefinition<ValueEventData<'data, 'args>> =

        let key =
            SimpleScalarAttributeDefinition.CreateAttributeData(
                ScalarAttributeComparers.noCompare,
                (fun oldValueOpt (newValueOpt: ValueEventData<'data, 'args> voption) node ->
                    let target = node.Target :?> BindableObject
                    let event = getEvent target

                    match newValueOpt with
                    | ValueNone ->
                        // The attribute is no longer applied, so we clean up the event
                        match node.TryGetHandler(name) with
                        | ValueNone -> ()
                        | ValueSome handler -> event.RemoveHandler(handler)

                        // Only clear the property if a value was set before
                        match oldValueOpt with
                        | ValueNone -> ()
                        | ValueSome _ -> target.ClearValue(bindableProperty)

                    | ValueSome curr ->
                        // Clean up the old event handler if any
                        match node.TryGetHandler(name) with
                        | ValueNone -> ()
                        | ValueSome handler -> event.RemoveHandler(handler)

                        // Set the new value
                        target.SetValue(bindableProperty, curr.Value)

                        // Set the new event handler
                        let handler =
                            EventHandler<'args>
                                (fun _ args ->
                                    let r = curr.Event args
                                    Dispatcher.dispatch node r)

                        node.SetHandler(name, ValueSome handler)
                        event.AddHandler(handler))
            )
            |> AttributeDefinitionStore.registerScalar

        { Key = key; Name = name }
