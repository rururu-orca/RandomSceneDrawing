namespace RandomSceneDrawing

open System
open System.Runtime.InteropServices
open System.Windows
open System.Windows.Automation.Peers
open System.Windows.Input
open System.Windows.Interop
open Windows.UI.Composition

[<ComImport>]
[<Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")>]
[<InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type ICompositorDesktopInterop =
    abstract CreateDesktopWindowTarget : nativeint * bool * nativeint outref -> unit

[<ComImport>]
[<Guid("A1BEA8BA-D726-4663-8129-6B5E7927FFA6")>]
[<InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type ICompositionTarget =
    abstract member Root : Windows.UI.Composition.Visual with get, set

module Native =
    [<Literal>]
    let WS_CHILD = 0x40000000

    [<Literal>]
    let WS_VISIBLE = 0x10000000

    [<Literal>]
    let LBS_NOTIFY = 0x00000001

    [<Literal>]
    let HOST_ID = 0x00000002

    [<Literal>]
    let LISTBOX_ID = 0x00000001

    [<Literal>]
    let WS_VSCROLL = 0x00200000

    [<Literal>]
    let WS_BORDER = 0x00800000

    type DISPATCHERQUEUE_THREAD_APARTMENTTYPE =
        | DQTAT_COM_NONE = 0
        | DQTAT_COM_ASTA = 1
        | DQTAT_COM_STA = 2

    type DISPATCHERQUEUE_THREAD_TYPE =
        | DQTYPE_THREAD_DEDICATED = 1
        | DQTYPE_THREAD_CURRENT = 2

    [<StructLayout(LayoutKind.Sequential)>]
    type DispatcherQueueOptions =
        { dwSize: int
          [<MarshalAs(UnmanagedType.I4)>]
          threadType: DISPATCHERQUEUE_THREAD_TYPE
          [<MarshalAs(UnmanagedType.I4)>]
          apartmentType: DISPATCHERQUEUE_THREAD_APARTMENTTYPE }

    [<DllImport("coremessaging.dll", EntryPoint = "CreateDispatcherQueueController", CharSet = CharSet.Unicode)>]
    extern IntPtr CreateDispatcherQueueController(DispatcherQueueOptions options, [<MarshalAs(UnmanagedType.IUnknown)>] obj& dispatcherQueueController)


    [<DllImportAttribute("user32.dll", EntryPoint = "CreateWindowEx", CharSet = CharSet.Unicode)>]
    extern nativeint CreateWindowEx(  int dwExStyle,
                                      string lpszClassName,
                                      string lpszWindowName,
                                      int style,
                                      int x, int y,
                                      int width, int height,
                                      nativeint hwndParent,
                                      nativeint hMenu,
                                      nativeint hInst,
                                      [<MarshalAs(UnmanagedType.AsAny)>] obj pvParam)

    [<DllImport("user32.dll", EntryPoint = "DestroyWindow", CharSet = CharSet.Unicode)>]
    extern bool DestroyWindow(nativeint hwnd)

open Native

type CompositionHost(height: double, width: double) =
    inherit HwndHost()

    let hostHeight, hostWidth = int height, int width

    [<DefaultValue>]
    val mutable hwndHost: nativeint

    [<DefaultValue>]
    val mutable compositionTarget: ICompositionTarget

    [<DefaultValue>]
    val mutable dispatcherQueue: obj

    let initializeCoreDispatcher () =
        let options =
            { apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA
              threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT
              dwSize = Marshal.SizeOf typeof<DispatcherQueueOptions> }

        let mutable queue = null

        CreateDispatcherQueueController(options, &queue)
        |> ignore

        queue

    member val Compositor: Compositor = null with get, set

    member this.Child
        with set (value) =
            if isNull this.Compositor then
                this.InitComposition this.hwndHost

            this.compositionTarget.Root <- value

    member this.InitComposition hwndHost =
        this.Compositor <- new Compositor()

        let interop = box this.Compositor :?> ICompositorDesktopInterop
        let mutable raw = IntPtr.Zero
        interop.CreateDesktopWindowTarget(hwndHost,true,&raw)

        let target = Marshal.GetObjectForIUnknown raw :?> ICompositionTarget

        ()

    override this.BuildWindowCore(hwndParent: HandleRef) : HandleRef =
        let hwndHost =
            CreateWindowEx(
                0,
                "static",
                "",
                WS_CHILD ||| WS_VISIBLE,
                0,
                0,
                hostWidth,
                hostHeight,
                hwndParent.Handle,
                nativeint HOST_ID,
                IntPtr.Zero,
                0
            )

        this.dispatcherQueue <- initializeCoreDispatcher ()
        this.InitComposition(hwndHost)

        HandleRef(this, hwndHost)

    override this.DestroyWindowCore(hwnd: HandleRef) : unit =
        if not <| isNull this.compositionTarget.Root then
            this.compositionTarget.Root.Dispose()

        DestroyWindow hwnd.Handle |> ignore


    override this.Dispose(disposing: bool) : unit = failwith "Not Implemented"
    override this.Finalize() : unit = failwith "Not Implemented"
    override this.HasFocusWithinCore() : bool = failwith "Not Implemented"
    override this.MeasureOverride(``constraint``: Size) : Size = failwith "Not Implemented"
    override this.OnCreateAutomationPeer() : AutomationPeer = failwith "Not Implemented"
    override this.OnDpiChanged(oldDpi: DpiScale, newDpi: DpiScale) : unit = failwith "Not Implemented"
    override this.OnKeyDown(e: KeyEventArgs) : unit = failwith "Not Implemented"
    override this.OnKeyUp(e: KeyEventArgs) : unit = failwith "Not Implemented"
    override this.OnMnemonicCore(msg: byref<MSG>, modifiers: ModifierKeys) : bool = failwith "Not Implemented"
    override this.OnWindowPositionChanged(rcBoundingBox: Rect) : unit = failwith "Not Implemented"

    override this.RegisterKeyboardInputSinkCore(sink: IKeyboardInputSink) : IKeyboardInputSite =
        failwith "Not Implemented"

    override this.TabIntoCore(request: TraversalRequest) : bool = failwith "Not Implemented"

    override this.TranslateAcceleratorCore(msg: byref<MSG>, modifiers: ModifierKeys) : bool = failwith "Not Implemented"

    override this.TranslateCharCore(msg: byref<MSG>, modifiers: ModifierKeys) : bool = failwith "Not Implemented"

    override this.WndProc
        (
            hwnd: nativeint,
            msg: int,
            wParam: nativeint,
            lParam: nativeint,
            handled: byref<bool>
        ) : nativeint =
        failwith "Not Implemented"
