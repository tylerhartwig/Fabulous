﻿namespace Fabulous.XamarinForms

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Fabulous
open Fabulous.StackAllocatedCollections.StackList
open Fabulous.StackAllocatedCollections
open Fabulous.XamarinForms

type IMarker = interface end
type IApplication = inherit IMarker
type IPage = inherit IMarker
type IView = inherit IMarker
type ICell = inherit IMarker
type IMenuItem = inherit IMarker
type IGestureRecognizer = inherit IMarker
type ILayout = inherit IView
type IToolbarItem = inherit IMenuItem

module Widgets =
    let registerWithAdditionalSetup<'T when 'T :> Xamarin.Forms.BindableObject and 'T: (new : unit -> 'T)> (additionalSetup: 'T -> IViewNode -> unit) =
        let key = WidgetDefinitionStore.getNextKey()

        let definition =
            {
                Key = key
                Name = typeof<'T>.Name
                GetTargetType = fun _ -> typeof<'T>
                CreateView =
                    fun (widget, treeContext, parentNode) ->
                        let name = typeof<'T>.Name
                        printfn $"Creating view for {name}"

                        let view = new 'T()
                        let weakReference = WeakReference(view)
                        
                        let node = ViewNode(parentNode, treeContext, weakReference)
                        ViewNode.set node view
                        
                        additionalSetup view node

                        Reconciler.update treeContext.CanReuseView ValueNone widget node

                        struct (node, box view)
            }

        WidgetDefinitionStore.set key definition
        key
        
    let register<'T when 'T :> Xamarin.Forms.BindableObject and 'T: (new : unit -> 'T)> () =
        registerWithAdditionalSetup<'T> (fun _ _ -> ())
    
[<Extension>]
type CollectionBuilderExtensions =
    [<Extension>]
    static member inline Yield<'msg, 'marker, 'itemType when 'itemType :> IPage>
        (
            _: CollectionBuilder<'msg, 'marker, IPage>,
            x: WidgetBuilder<'msg, 'itemType>
        ) : Content<'msg> =
        {
            Widgets = MutStackArray1.One(x.Compile())
        }

    [<Extension>]
    static member inline Yield<'msg, 'marker, 'itemType when 'itemType :> IView>
        (
            _: CollectionBuilder<'msg, 'marker, IView>,
            x: WidgetBuilder<'msg, 'itemType>
        ) : Content<'msg> =
        {
            Widgets = MutStackArray1.One(x.Compile())
        }

    [<Extension>]
    static member inline Yield<'msg, 'marker, 'itemType when 'itemType :> IGestureRecognizer>
        (
            _: AttributeCollectionBuilder<'msg, 'marker, IGestureRecognizer>,
            x: WidgetBuilder<'msg, 'itemType>
        ) : Content<'msg> =
        {
            Widgets = MutStackArray1.One(x.Compile())
        }

    [<Extension>]
    static member inline Yield<'msg, 'marker, 'itemType when 'itemType :> IToolbarItem>
        (
            _: AttributeCollectionBuilder<'msg, 'marker, IToolbarItem>,
            x: WidgetBuilder<'msg, 'itemType>
        ) : Content<'msg> =
        {
            Widgets = MutStackArray1.One(x.Compile())
        }
        
    
        
    [<Extension>]
    static member inline Yield<'msg, 'marker, 'itemType when 'itemType :> IPage>(_: CollectionBuilder<'msg, 'marker, IPage>, x: WidgetBuilder<'msg, Memo.Memoized<'itemType>>) : Content<'msg> =
        { Widgets = MutStackArray1.One(x.Compile()) }
    [<Extension>]
    static member inline Yield<'msg, 'marker, 'itemType when 'itemType :> IView>(_: CollectionBuilder<'msg, 'marker, IView>, x: WidgetBuilder<'msg, Memo.Memoized<'itemType>>) : Content<'msg> =
        { Widgets = MutStackArray1.One(x.Compile()) }
    [<Extension>]
    static member inline Yield<'msg, 'marker, 'itemType when 'itemType :> IGestureRecognizer>(_: AttributeCollectionBuilder<'msg, 'marker, IGestureRecognizer>, x: WidgetBuilder<'msg, Memo.Memoized<'itemType>>) : Content<'msg> =
        { Widgets = MutStackArray1.One(x.Compile()) }
    [<Extension>]
    static member inline Yield<'msg, 'marker, 'itemType when 'itemType :> IToolbarItem>(_: AttributeCollectionBuilder<'msg, 'marker, IToolbarItem>, x: WidgetBuilder<'msg, Memo.Memoized<'itemType>>) : Content<'msg> =
        { Widgets = MutStackArray1.One(x.Compile()) }

module ViewHelpers =
    let inline compileSeq (items: seq<WidgetBuilder<'msg, #IMarker>>) =
        items
        |> Seq.map (fun item -> item.Compile())
        |> Seq.toArray

    let inline buildWidgets<'msg, 'marker> (key: WidgetKey) (attrs: WidgetAttribute []) =
        WidgetBuilder<'msg, 'marker>(key, struct (StackList.empty(), ValueSome attrs, ValueNone))

    let inline buildAttributeCollection<'msg, 'marker, 'item>
        (collectionAttributeDefinition: WidgetCollectionAttributeDefinition)
        (widget: WidgetBuilder<'msg, 'marker>)
        =
        AttributeCollectionBuilder<'msg, 'marker, 'item>(widget, collectionAttributeDefinition)
        
    let buildItem<'msg, 'marker, 'itemData, 'itemMarker> (key: WidgetKey) (itemsAttributeDefinition: ScalarAttributeDefinition<WidgetItems, WidgetItems, IEnumerable<Widget>>) (items: seq<'itemData>) (itemTemplate: 'itemData -> WidgetBuilder<'msg, 'itemMarker>) =
        let itemTemplate (item: obj) =
            (itemTemplate (unbox<'itemData> item)).Compile()
        
        let attrs =
            [| itemsAttributeDefinition.WithValue({ Items = items; Template = box >> itemTemplate }) |]
        
        WidgetBuilder<'msg, 'marker>(key, struct (attrs, [||], [||]))