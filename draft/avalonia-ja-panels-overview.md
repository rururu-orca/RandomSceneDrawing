# Panels Overview

パネル要素は、要素のレンダリング（サイズや寸法、位置、子コンテンツの配置）を制御するコンポーネントです。Avalonia では、多くの定義済み `Panel` 要素と、カスタム `Panel` 要素を作成することができます。

## The Panel Class <a id="the-panel-class"></a>

`Panel`は、Avaloniaのレイアウトサポートを提供するすべての要素の基本クラスです。派生した `Panel` 要素は、XAML やコード内の要素を配置するために使用されます。

アヴァロニアには、多くの複雑なレイアウトを可能にする、派生パネル実装の包括的なスイートが含まれています。これらの派生クラスは、ほとんどの標準的なUIシナリオを可能にするプロパティとメソッドを公開します。ニーズに合った子実装を見つけられない開発者は、`ArrangeOverride`と`MeasureOverride`メソッドをオーバーライドして、新しいレイアウトを作成することができます。カスタムレイアウトビヘイビアについての詳細は、 [Create a Custom Panel](create-a-custom-panel.md) を参照してください。

### Panel Common Members <a id="panel-common-members"></a>

すべての `Panel` 要素は `Control` で定義された、`Height`, `Width`, `HorizontalAlignment`, `VerticalAlignment`, `Margin` といった基本的なサイズと位置のプロパティをサポートします。`Control` で定義される位置決めプロパティの詳細については、[Alignment, Margins, and Padding Overview](alignment-margins-and-padding.md) を参照してください。

`Panel` は、レイアウトを理解し、使用する上で非常に重要な追加のプロパティを公開しています。`Background` プロパティは、派生したパネル要素の境界線を `Brush` で塗りつぶすために使用されます。`Children` は `Panel` が構成するエレメントの子コレクションを表します。

**Attached Properties**

派生パネル要素では、アタッチドプロパティが多用されています。Attached プロパティは、従来の Common Language Runtime\\(CLR) プロパティの "wrapper" を持たない、特殊な形式の依存プ ロパティです。Attached プロパティは、XAML で特殊な構文を持っており、この後のいくつかの例で見ることができます。

アタッチド・プロパティの目的の1つは、実際には親要素によって定義されるプロパティの一意の値を子要素に保存できるようにすることです。この機能の応用として、子要素がUIでどのように表示されたいかを親要素に知らせることができ、アプリケーションのレイアウトに非常に有効です。

### User Interface Panels <a id="user-interface-panels"></a>

Avaloniaでは、UIシナリオをサポートするために最適化されたパネルクラスがいくつか用意されています。`Panel`, `Canvas`, `DockPanel`, `Grid`, `StackPanel`, `WrapPanel` と `RelativePanel` です。これらのパネルエレメントは使いやすく、汎用性があり、ほとんどのアプリケーションに対応できるよう拡張されています。

**Canvas**

`Canvas` 要素は、絶対座標 _x-_ と _y-_ に従ってコンテンツの位置決めを行うことができます。要素は固有の位置に描画することができます。または、要素が同じ座標を占める場合、マークアップに現れる順序によって要素の描画順序が決まります。

`Canvas` は `Panel` の中で最も柔軟なレイアウトのサポートを提供します。高さと幅のプロパティはキャンバスの領域を定義するために使用され、内部の要素は親の `Canvas` の領域からの相対的な絶対座標が割り当てられます。4つのプロパティ、 `Canvas.Left` 、 `Canvas.Top` 、 `Canvas.Right` 、 `Canvas.Bottom` は `Canvas` 内のオブジェクトの配置を細かく制御することができ、開発者はスクリーン上に正確に要素を配置することができます。

**ClipToBounds Within a Canvas**

`Canvas` は画面上の任意の位置に子要素を配置することができます。たとえ、自身が定義した `Height` と `Width` の座標から外れていても大丈夫です。さらに、 `Canvas` は子要素のサイズに影響されません。その結果、子要素が親要素である `Canvas` の外接矩形の外側にある他の要素を過剰に描画する可能性があります。`Canvas` のデフォルトの動作は、親である `Canvas` の境界の外側に子要素を描画することを許可するものです。この動作が好ましくない場合は、 `ClipToBounds` プロパティを `true` に設定することができます。これにより、 `Canvas` は自身のサイズにクリップされます。`Canvas` は子要素をその境界の外に描画することができる唯一のレイアウト要素です。

**Defining and Using a Canvas**

キャンバス（`Canvas`）は、XAMLやコードを使って簡単にインスタンス化することができます。次の例は、`Canvas` を使ってコンテンツを絶対配置する方法を示しています。このコードでは、100ピクセルの正方形を3つ作成します。最初の正方形は赤で、左上の \(_x, y_) の位置は \(0, 0) と指定されています。2つ目の正方形は緑で、左上の位置は1つ目の正方形のすぐ下、右側の位置で、 \(100, 100)と指定されます。3つ目の正方形は青色で、左上の位置は "week"（50, 50）であり、1つ目の正方形の右下と2つ目の正方形の左上を囲んでいる。3つ目の正方形は最後に置かれたので、他の2つの正方形の上に乗っているように見える。つまり、重なった部分は3つ目の箱の色になる。

C\#

```csharp
// Create the Canvas
myParentCanvas = new Canvas();
myParentCanvas.Width = 400;
myParentCanvas.Height = 400;
// Define child Canvas elements
myCanvas1 = new Canvas();
myCanvas1.Background = Brushes.Red;
myCanvas1.Height = 100;
myCanvas1.Width = 100;
Canvas.SetTop(myCanvas1, 0);
Canvas.SetLeft(myCanvas1, 0);
myCanvas2 = new Canvas();
myCanvas2.Background = Brushes.Green;
myCanvas2.Height = 100;
myCanvas2.Width = 100;
Canvas.SetTop(myCanvas2, 100);
Canvas.SetLeft(myCanvas2, 100);
myCanvas3 = new Canvas();
myCanvas3.Background = Brushes.Blue;
myCanvas3.Height = 100;
myCanvas3.Width = 100;
Canvas.SetTop(myCanvas3, 50);
Canvas.SetLeft(myCanvas3, 50);
// Add child elements to the Canvas' Children collection
myParentCanvas.Children.Add(myCanvas1);
myParentCanvas.Children.Add(myCanvas2);
myParentCanvas.Children.Add(myCanvas3);
```

XAML

```markup
<Canvas Height="400" Width="400">
  <Canvas Height="100" Width="100" Top="0" Left="0" Background="Red"/>
  <Canvas Height="100" Width="100" Top="100" Left="100" Background="Green"/>
  <Canvas Height="100" Width="100" Top="50" Left="50" Background="Blue"/>
</Canvas>
```

**DockPanel**

`DockPanel` 要素は、子コンテンツ要素に設定された `DockPanel.Dock` 添付プロパティを使用して、コンテナの縁に沿ったコンテンツの配置を行います。`DockPanel.Dock`が `Top` または `Bottom` に設定されている場合、子要素を上または下に配置することができます。`DockPanel.Dock`が `Left` または `Right` に設定されている場合、子要素を左または右へ配置することができます。`LastChildFill` プロパティは `DockPanel` の子要素として追加される最後の要素の位置を決定します。

`DockPanel` を使用すると、ボタンのセットのような関連するコントロールのグループを配置することができます。また、"paneed" UIを作成するために使用することもできます。

**Sizing to Content**

`Height`と`Width`のプロパティが指定されていない場合、`DockPanel`はその内容に合わせてサイズを調整します。サイズは子要素の大きさに合わせて増減します。ただし、これらのプロパティが指定され、次に指定された子要素のためのスペースがなくなった場合、`DockPanel` はその子要素や後続の子要素を表示せず、後続の子要素のサイズを測定しません。

**LastChildFill**

デフォルトでは、`DockPanel` 要素の最後の子は、残りの未割り当てのスペースを "埋める" ことになります。この動作が必要ない場合は、`LastChildFill` プロパティを `false` に設定してください。

**Defining and Using a DockPanel**

次の例は、`DockPanel` を使ってスペースを分割する方法を示しています。5つの `Border` 要素が親である `DockPanel` の子として追加されています。それぞれは `DockPanel` の異なるポジショニングのプロパティを使用してスペースを分割しています。最後の要素は残りの未割り当てのスペースを "埋める "ものです。

C\#

```csharp
// Create the DockPanel
DockPanel myDockPanel = new DockPanel();
myDockPanel.LastChildFill = true;
// Define the child content
Border myBorder1 = new Border();
myBorder1.Height = 25;
myBorder1.Background = Brushes.SkyBlue;
myBorder1.BorderBrush = Brushes.Black;
myBorder1.BorderThickness = new Thickness(1);
DockPanel.SetDock(myBorder1, Dock.Top);
TextBlock myTextBlock1 = new TextBlock();
myTextBlock1.Foreground = Brushes.Black;
myTextBlock1.Text = "Dock = Top";
myBorder1.Child = myTextBlock1;
Border myBorder2 = new Border();
myBorder2.Height = 25;
myBorder2.Background = Brushes.SkyBlue;
myBorder2.BorderBrush = Brushes.Black;
myBorder2.BorderThickness = new Thickness(1);
DockPanel.SetDock(myBorder2, Dock.Top);
TextBlock myTextBlock2 = new TextBlock();
myTextBlock2.Foreground = Brushes.Black;
myTextBlock2.Text = "Dock = Top";
myBorder2.Child = myTextBlock2;
Border myBorder3 = new Border();
myBorder3.Height = 25;
myBorder3.Background = Brushes.LemonChiffon;
myBorder3.BorderBrush = Brushes.Black;
myBorder3.BorderThickness = new Thickness(1);
DockPanel.SetDock(myBorder3, Dock.Bottom);
TextBlock myTextBlock3 = new TextBlock();
myTextBlock3.Foreground = Brushes.Black;
myTextBlock3.Text = "Dock = Bottom";
myBorder3.Child = myTextBlock3;
Border myBorder4 = new Border();
myBorder4.Width = 200;
myBorder4.Background = Brushes.PaleGreen;
myBorder4.BorderBrush = Brushes.Black;
myBorder4.BorderThickness = new Thickness(1);
DockPanel.SetDock(myBorder4, Dock.Left);
TextBlock myTextBlock4 = new TextBlock();
myTextBlock4.Foreground = Brushes.Black;
myTextBlock4.Text = "Dock = Left";
myBorder4.Child = myTextBlock4;
Border myBorder5 = new Border();
myBorder5.Background = Brushes.White;
myBorder5.BorderBrush = Brushes.Black;
myBorder5.BorderThickness = new Thickness(1);
TextBlock myTextBlock5 = new TextBlock();
myTextBlock5.Foreground = Brushes.Black;
myTextBlock5.Text = "This content will Fill the remaining space";
myBorder5.Child = myTextBlock5;
// Add child elements to the DockPanel Children collection
myDockPanel.Children.Add(myBorder1);
myDockPanel.Children.Add(myBorder2);
myDockPanel.Children.Add(myBorder3);
myDockPanel.Children.Add(myBorder4);
myDockPanel.Children.Add(myBorder5);
```

XAML

```markup
<DockPanel LastChildFill="True">
  <Border Height="25" Background="SkyBlue" BorderBrush="Black" BorderThickness="1" DockPanel.Dock="Top">
    <TextBlock Foreground="Black">Dock = "Top"</TextBlock>
  </Border>
  <Border Height="25" Background="SkyBlue" BorderBrush="Black" BorderThickness="1" DockPanel.Dock="Top">
    <TextBlock Foreground="Black">Dock = "Top"</TextBlock>
  </Border>
  <Border Height="25" Background="LemonChiffon" BorderBrush="Black" BorderThickness="1" DockPanel.Dock="Bottom">
    <TextBlock Foreground="Black">Dock = "Bottom"</TextBlock>
  </Border>
  <Border Width="200" Background="PaleGreen" BorderBrush="Black" BorderThickness="1" DockPanel.Dock="Left">
    <TextBlock Foreground="Black">Dock = "Left"</TextBlock>
  </Border>
  <Border Background="White" BorderBrush="Black" BorderThickness="1">
    <TextBlock Foreground="Black">This content will "Fill" the remaining space</TextBlock>
  </Border>
</DockPanel>
```

**Grid**

`Grid` 要素は、絶対位置制御と表形式データコントロールの機能を統合したものです。`Grid` を使用すると、要素を簡単に配置したり、スタイルを変更したりすることができます。`Grid` では、行や列のグループ化を柔軟に定義することができ、さらに、複数の `Grid` 要素間でサイズ情報を共有するメカニズムも提供されています。

**Sizing Behavior of Columns and Rows**

`Grid`の中で定義された列や行は、残りのスペースを比例配分するために、`Star`サイジングを活用することができます。行や列の高さや幅として `Star` が選択された場合、その列や行は残りの利用可能なスペースの比率を重み付けして受け取ります。これは、列や行のコンテンツのサイズに基づいてスペースを均等に配分する `Auto` とは対照的です。この値は、XAMLでは `*` または `2*` として表現されます。最初のケースでは、行またはカラムは利用可能なスペースの1倍、2番目のケースでは2倍、といった具合に受け取られます。このスペースを比例配分するテクニックと `HorizontalAlignment` と `VerticalAlignment` の値 `Stretch` を組み合わせることで、スクリーンスペースに対する割合でレイアウトスペースを分割することが可能になります。このような方法でスペースを分割することができるレイアウトパネルは `Grid` だけです。

**Defining and Using a Grid**

次の例は、Windowsのスタートメニューにある「ファイル名を指定して実行」ダイアログと同様のUIを構築する方法を示しています。

C\#

```csharp
// Create the Grid.
grid1 = new Grid ();
grid1.Background = Brushes.Gainsboro;
grid1.HorizontalAlignment = HorizontalAlignment.Left;
grid1.VerticalAlignment = VerticalAlignment.Top;
grid1.ShowGridLines = true;
grid1.Width = 425;
grid1.Height = 165;
// Define the Columns.
colDef1 = new ColumnDefinition();
colDef1.Width = new GridLength(1, GridUnitType.Auto);
colDef2 = new ColumnDefinition();
colDef2.Width = new GridLength(1, GridUnitType.Star);
colDef3 = new ColumnDefinition();
colDef3.Width = new GridLength(1, GridUnitType.Star);
colDef4 = new ColumnDefinition();
colDef4.Width = new GridLength(1, GridUnitType.Star);
colDef5 = new ColumnDefinition();
colDef5.Width = new GridLength(1, GridUnitType.Star);
grid1.ColumnDefinitions.Add(colDef1);
grid1.ColumnDefinitions.Add(colDef2);
grid1.ColumnDefinitions.Add(colDef3);
grid1.ColumnDefinitions.Add(colDef4);
grid1.ColumnDefinitions.Add(colDef5);
// Define the Rows.
rowDef1 = new RowDefinition();
rowDef1.Height = new GridLength(1, GridUnitType.Auto);
rowDef2 = new RowDefinition();
rowDef2.Height = new GridLength(1, GridUnitType.Auto);
rowDef3 = new RowDefinition();
rowDef3.Height = new GridLength(1, GridUnitType.Star);
rowDef4 = new RowDefinition();
rowDef4.Height = new GridLength(1, GridUnitType.Auto);
grid1.RowDefinitions.Add(rowDef1);
grid1.RowDefinitions.Add(rowDef2);
grid1.RowDefinitions.Add(rowDef3);
grid1.RowDefinitions.Add(rowDef4);
// Add the Image.
img1 = new Image();
img1.Source = runicon;
Grid.SetRow(img1, 0);
Grid.SetColumn(img1, 0);
// Add the main application dialog.
txt1 = new TextBlock();
txt1.Text = "Type the name of a program, folder, document, or Internet resource, and Windows will open it for you.";
txt1.TextWrapping = TextWrapping.Wrap;
Grid.SetColumnSpan(txt1, 4);
Grid.SetRow(txt1, 0);
Grid.SetColumn(txt1, 1);
// Add the second text cell to the Grid.
txt2 = new TextBlock();
txt2.Text = "Open:";
Grid.SetRow(txt2, 1);
Grid.SetColumn(txt2, 0);
// Add the TextBox control.
tb1 = new TextBox();
Grid.SetRow(tb1, 1);
Grid.SetColumn(tb1, 1);
Grid.SetColumnSpan(tb1, 5);
// Add the buttons.
button1 = new Button();
button2 = new Button();
button3 = new Button();
button1.Content = "OK";
button2.Content = "Cancel";
button3.Content = "Browse ...";
Grid.SetRow(button1, 3);
Grid.SetColumn(button1, 2);
button1.Margin = new Thickness(10, 0, 10, 15);
button2.Margin = new Thickness(10, 0, 10, 15);
button3.Margin = new Thickness(10, 0, 10, 15);
Grid.SetRow(button2, 3);
Grid.SetColumn(button2, 3);
Grid.SetRow(button3, 3);
Grid.SetColumn(button3, 4);
grid1.Children.Add(img1);
grid1.Children.Add(txt1);
grid1.Children.Add(txt2);
grid1.Children.Add(tb1);
grid1.Children.Add(button1);
grid1.Children.Add(button2);
grid1.Children.Add(button3);
```

**StackPanel**

`StackPanel` は指定された方向にエレメントを "スタック" することができます。デフォルトのスタック方向は垂直です。`Orientation` プロパティはコンテンツの流れを制御するために使用することができます。

**StackPanel vs. DockPanel**

`DockPanel` は子要素を "スタック" することもできますが、 `DockPanel` と `StackPanel` はいくつかの使用シナリオで類似の結果を得ることができません。例えば、子要素の順番は `DockPanel` ではそのサイズに影響を与えますが、 `StackPanel` では影響を与えません。これは `StackPanel` が `PositiveInfinity` でスタッキングの方向を測定するのに対し、 `DockPanel` は利用可能なサイズのみを測定するからです。

**Defining and Using a StackPanel**

次の例は、`StackPanel` を使って、縦に配置されたボタンのセットを作成する方法を示しています。水平に配置する場合は、`Orientation` プロパティを `Horizontal` に設定します。

C\#

```csharp
// Define the StackPanel
myStackPanel = new StackPanel();
myStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
myStackPanel.VerticalAlignment = VerticalAlignment.Top;
// Define child content
Button myButton1 = new Button();
myButton1.Content = "Button 1";
Button myButton2 = new Button();
myButton2.Content = "Button 2";
Button myButton3 = new Button();
myButton3.Content = "Button 3";
// Add child elements to the parent StackPanel
myStackPanel.Children.Add(myButton1);
myStackPanel.Children.Add(myButton2);
myStackPanel.Children.Add(myButton3);
```

**WrapPanel**

`WrapPanel` は子要素を左から右へ順次配置するために使用され、親コンテナの端に達したときにコンテンツを次の行に分割します。コンテンツは水平方向にも垂直方向にも配置することができます。`WrapPanel` はシンプルなフローティングUIシナリオに便利です。また、すべての子要素に均一なサイズを適用するために使用することができます。

次の例では、`WrapPanel` を作成して、`Button` コントロールがコンテナの端に到達したときにラップするように表示する方法を示しています。

C\#

```csharp
// Instantiate a new WrapPanel and set properties
myWrapPanel = new WrapPanel();
myWrapPanel.Background = System.Windows.Media.Brushes.Azure;
myWrapPanel.Orientation = Orientation.Horizontal;
myWrapPanel.Width = 200;
myWrapPanel.HorizontalAlignment = HorizontalAlignment.Left;
myWrapPanel.VerticalAlignment = VerticalAlignment.Top;
// Define 3 button elements. The last three buttons are sized at width 
// of 75, so the forth button wraps to the next line.
btn1 = new Button();
btn1.Content = "Button 1";
btn1.Width = 200;
btn2 = new Button();
btn2.Content = "Button 2";
btn2.Width = 75;
btn3 = new Button();
btn3.Content = "Button 3";
btn3.Width = 75;
btn4 = new Button();
btn4.Content = "Button 4";
btn4.Width = 75;
// Add the buttons to the parent WrapPanel using the Children.Add method.
myWrapPanel.Children.Add(btn1);
myWrapPanel.Children.Add(btn2);
myWrapPanel.Children.Add(btn3);
myWrapPanel.Children.Add(btn4);
```

XAML

```markup
<Border HorizontalAlignment="Left" VerticalAlignment="Top" BorderBrush="Black" BorderThickness="2">
  <WrapPanel Background="LightBlue" Width="200" Height="100">
    <Button Width="200">Button 1</Button>
    <Button>Button 2</Button>
    <Button>Button 3</Button>
    <Button>Button 4</Button>
  </WrapPanel>
</Border>
```

### Nested Panel Elements <a id="nested-panel-elements"></a>

パネル(Panel)要素は、複雑なレイアウトを作成するために、互いに入れ子にすることができます。これは、1つの `Panel` がUIの一部には理想的であっても、UIの別の部分のニーズを満たせないような状況で、非常に便利であることがわかります。

アプリケーションがサポートできるネストの量に現実的な制限はありませんが、一般的には、希望するレイアウトに実際に必要なパネルだけを使用するようにアプリケーションを制限することが最善です。多くの場合、レイアウトコンテナとしての柔軟性から、ネストされたパネルの代わりに `Grid` 要素を使用することができます。これは、不要な要素をツリーから排除することで、アプリケーションのパフォーマンスを向上させることができます。
