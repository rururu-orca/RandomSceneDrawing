# Alignment, Margins and Padding

Avaloniaコントロールは、子要素を正確に配置するために、いくつかのプロパティを公開しています。このトピックでは、最も重要な4つのプロパティについて説明します。`HorizontalAlignment`、`Margin`、`Padding`、`VerticalAlignment`です。これらのプロパティの効果は、アバロニア・アプリケーションで要素の位置を制御するための基礎となるものなので、理解しておくことが重要です。

### Introduction to Element Positioning <a id="introduction-to-element-positioning"></a>

アバロニアには、さまざまな要素の配置方法があります。しかし、理想的なレイアウトを実現するには、単に適切な`パネル`要素を選択するだけでは不十分です。細かい位置の制御には、`HorizontalAlignment`、`Margin`、`Padding`、`VerticalAlignment`のプロパティを理解することが必要です。

次の図は、いくつかの位置決めプロパティを利用したレイアウトシナリオを示したものです。

![Positioning Example](../../.gitbook/assets/layout-margins-padding-alignment-graphic1.png)

この図の`Button`要素は、一見するとランダムに配置されているように見えるかもしれません。しかし、実際には、マージン、アラインメント、パディングを組み合わせて、その位置を正確に制御しているのです。

次の例では、先の図のようなレイアウトを作成する方法を説明します。`Border` 要素は親である `StackPanel` をカプセル化し、 `Padding` の値をデバイスに依存しない15ピクセルに設定しています。これは、子である `StackPanel` を囲む狭い `LightBlue` バンドを考慮したものです。`StackPanel` の子要素は、このトピックで説明する様々な位置決めプロパティのそれぞれを説明するために使用されます。3つの `Button` 要素を使って、 `Margin` と `HorizontalAlignment` の両プロパティを説明しています。

```markup
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="AvaloniaApplication2.MainWindow"
        Title="AvaloniaApplication2">
  <Border Background="LightBlue"
          BorderBrush="Black"
          BorderThickness="2"
          Padding="15">
    <StackPanel Background="White"
                HorizontalAlignment="Center"
                VerticalAlignment="Top">
      <TextBlock Margin="5,0"
                 FontSize="18"
                 HorizontalAlignment="Center">
        Alignment, Margin and Padding Sample
      </TextBlock>
      <Button HorizontalAlignment="Left" Margin="20">Button 1</Button>
      <Button HorizontalAlignment="Right" Margin="10">Button 2</Button>
      <Button HorizontalAlignment="Stretch">Button 3</Button>
    </StackPanel>
  </Border>
</Window>

```

次の図は、前述のサンプルで使用されている様々な位置決めプロパティをクローズアップしたものです。このトピックの後続のセクションでは、各位置決めプロパティの使用方法についてより詳細に説明します。

![Positioning Properties](../../.gitbook/assets/layout-margins-padding-alignment-graphic2.png)

### Understanding Alignment Properties <a id="understanding-alignment-properties"></a>

`HorizontalAlignment` と `VerticalAlignment` プロパティは、親要素に割り当てられたレイアウトスペースの中で子要素がどのように配置されるべきかを記述します。これらのプロパティを併用することで、子要素を正確に配置することができます。例えば、`DockPanel`の子要素は4つの異なる水平方向のアライメントを指定することができます。`Left`, `Right`, `Center`, または `Stretch` を指定して、利用可能なスペースに配置することができます。垂直方向の配置についても同様の値を指定することができます。

明示的に設定された `Height` と `Width` のプロパティは、 `Stretch` プロパティの値よりも優先されます。`Height` と `Width` を設定し、 `HorizontalAlignment` の値を `Stretch` に設定しようとすると、 `Stretch` リクエストは無視されます。

#### HorizontalAlignment Property <a id="horizontalalignment-property"></a>

`HorizontalAlignment` プロパティは、子要素に適用される水平方向のアライメント特性を宣言します。次の表は、 `HorizontalAlignment` プロパティに設定できる各値を示しています。

| Member | Description |
| :--- | :--- |
| `Left` | 子要素は、親要素に割り当てられたレイアウト空間の左側に配置される。 |
| `Center` | 子要素は、親要素に割り当てられたレイアウト空間の中心に整列されます。 |
| `Right` | 子要素は、親要素に割り当てられたレイアウト空間の右側に配置される。 |
| `Stretch` \(Default\) | 子要素は、親要素に割り当てられたレイアウト空間を埋めるように引き伸ばされます。明示的に指定された `Width` と `Height` の値が優先されます。 |

次の例では、`Button` 要素に `HorizontalAlignment` プロパティを適用する方法を示しています。各属性値は、様々なレンダリング動作をより良く説明するために、表示されています。

```markup
<Button HorizontalAlignment="Left">Button 1 (Left)</Button>
<Button HorizontalAlignment="Right">Button 2 (Right)</Button>
<Button HorizontalAlignment="Center">Button 3 (Center)</Button>
<Button HorizontalAlignment="Stretch">Button 4 (Stretch)</Button>
```

上記のコードを実行すると、次の画像のようなレイアウトになります。各 `HorizontalAlignment` 値の配置効果は、図の中で見ることができます。

![HorizontalAlignment Sample](../../.gitbook/assets/layout-horizontal-alignment-graphic.png)

#### VerticalAlignment Property <a id="verticalalignment-property"></a>

`VerticalAlignment` プロパティは、子要素に適用される垂直方向のアライメント特性を記述します。以下の表は、 `VerticalAlignment` プロパティに設定可能な各値を示しています。

| Member | Description |
| :--- | :--- |
| `Top` | 子要素は、親要素に割り当てられたレイアウト空間の上部に整列される。 |
| `Center` | 子要素は、親要素に割り当てられたレイアウト空間の中心に整列されます。 |
| `Bottom` | 子要素は、親要素に割り当てられたレイアウト空間の底辺に整列されます。 |
| `Stretch` \(Default\) | 子要素は、親要素に割り当てられたレイアウト空間を埋めるように引き伸ばされます。明示的に指定された `Width` と `Height` の値が優先されます。 |

次の例では、`Button`要素に `VerticalAlignment` プロパティを適用する方法を示しています。各属性値は、様々なレンダリング動作をより良く説明するために、表示されています。このサンプルでは、各プロパティ値のレイアウト動作をより良く説明するために、グリッドラインが表示されている `Grid` 要素を親として使用しています。

```markup
<Border Background="LightBlue" BorderBrush="Black" BorderThickness="2" Padding="15">
    <Grid Background="White" ShowGridLines="True">
      <Grid.RowDefinitions>
        <RowDefinition Height="25"/>
        <RowDefinition Height="50"/>
        <RowDefinition Height="50"/>
        <RowDefinition Height="50"/>
        <RowDefinition Height="50"/>
      </Grid.RowDefinitions>
      <TextBlock Grid.Row="0" Grid.Column="0"
                 FontSize="18"
                 HorizontalAlignment="Center">
        VerticalAlignment Sample
      </TextBlock>
      <Button Grid.Row="1" Grid.Column="0" VerticalAlignment="Top">Button 1 (Top)</Button>
      <Button Grid.Row="2" Grid.Column="0" VerticalAlignment="Bottom">Button 2 (Bottom)</Button>
      <Button Grid.Row="3" Grid.Column="0" VerticalAlignment="Center">Button 3 (Center)</Button>
      <Button Grid.Row="4" Grid.Column="0" VerticalAlignment="Stretch">Button 4 (Stretch)</Button>
    </Grid>
</Border>
```

上記のコードを実行すると、次の画像のようなレイアウトになります。それぞれの `VerticalAlignment` 値の配置効果は、図の中で見ることができます。

![VerticalAlignment property sample](../../.gitbook/assets/layout-vertical-alignment-graphic.png)

### Understanding Margin Properties <a id="understanding-margin-properties"></a>

`Margin` プロパティは、ある要素とその子または仲間との間の距離を記述します。`Margin` の値は、 `Margin="20"` のようなシンタックスを使用することで、一様な値にすることができます。このシンタックスでは、デバイスに依存しない20ピクセルの一様な `Margin` が要素に適用されます。`Margin` 値は、`Margin="0,10,5,25"` のように、4つの異なる値で構成されることも可能です。`Margin` プロパティを適切に使用することで、要素の描画位置、および隣接する要素や子の描画位置を非常に細かく制御することができます。

ゼロでないマージンは、要素の `Bounds` の外側にスペースを適用します。

次の例は、 `Button` 要素のグループの周りに均一なマージンを適用する方法を示しています。`Button` 要素は、各方向に10ピクセルのマージンバッファを持ち、等間隔に配置されています。

```markup
<Button Margin="10">Button 7</Button>
<Button Margin="10">Button 8</Button>
<Button Margin="10">Button 9</Button>
```

多くの場合、一様なマージンは適切ではありません。このような場合、非一様な間隔を適用することができる。次の例は、子要素に一様でないマージン間隔を適用する方法を示している。マージンは、左、上、右、下の順で記述します。

```markup
<Button Margin="0,10,0,10">Button 1</Button>
<Button Margin="0,10,0,10">Button 2</Button>
<Button Margin="0,10,0,10">Button 3</Button>
```

#### Understanding the Padding Property <a id="understanding-the-padding-property"></a>

Padding はほとんどの点で `Margin` に似ています。Padding プロパティはいくつかのクラスでのみ公開されており、主に便宜上公開されています。`Border`, `TemplatedControl`, `TextBlock` は Padding プロパティを公開しているクラスの例です。`Padding` プロパティは、指定された `Thickness` 値によって子要素の有効サイズを拡大します。

次の例は、親要素である `Border` に `Padding` を適用する方法を示しています。

```markup
<Border Background="LightBlue"
        BorderBrush="Black"
        BorderThickness="2"
        CornerRadius="45"
        Padding="25">
```

#### Using Alignment, Margins, and Padding in an Application <a id="using-alignment-margins-and-padding-in-an-application"></a>

`HorizontalAlignment`、`Margin`、`Padding`、`VerticalAlignment` は、複雑な UI を作成するために必要な位置制御を提供します。各プロパティの効果を利用して子要素の配置を変更することができ、動的なアプリケーションやユーザーエクスペリエンスを柔軟に作成することができます。

次の例は、このトピックで詳しく説明されている各コンセプトを実証するものです。この例では、このトピックの最初のサンプルで見つかったインフラストラクチャを基に、最初のサンプルの `Border` の子として `Grid` 要素を追加しています。親要素である `Border` には `Padding` が適用されます。`Grid` は3つの子要素 `StackPanel` の間のスペースを分割するために使用されます。`Margin` と `HorizontalAlignment` の様々な効果を表現するために、再び `Button` 要素が使用されています。各カラムの `Button` 要素に適用される様々なプロパティをより良く定義するために、`TextBlock` 要素が各 `ColumnDefinition` に追加されています。

```markup
<Border Background="LightBlue"
        BorderBrush="Black"
        BorderThickness="2"
        CornerRadius="45"
        Padding="25">
    <Grid Background="White" ShowGridLines="True">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>

    <StackPanel Grid.Column="0" Grid.Row="0"
                HorizontalAlignment="Left"
                Name="StackPanel1"
                VerticalAlignment="Top">
        <TextBlock FontSize="18" HorizontalAlignment="Center" Margin="0,0,0,15">StackPanel1</TextBlock>
        <Button Margin="0,10,0,10">Button 1</Button>
        <Button Margin="0,10,0,10">Button 2</Button>
        <Button Margin="0,10,0,10">Button 3</Button>
        <TextBlock>ColumnDefinition.Width="Auto"</TextBlock>
        <TextBlock>StackPanel.HorizontalAlignment="Left"</TextBlock>
        <TextBlock>StackPanel.VerticalAlignment="Top"</TextBlock>
        <TextBlock>StackPanel.Orientation="Vertical"</TextBlock>
        <TextBlock>Button.Margin="0,10,0,10"</TextBlock>
    </StackPanel>

    <StackPanel Grid.Column="1" Grid.Row="0"
                HorizontalAlignment="Stretch"
                Name="StackPanel2"
                VerticalAlignment="Top"
                Orientation="Vertical">
        <TextBlock FontSize="18" HorizontalAlignment="Center" Margin="0,0,0,15">StackPanel2</TextBlock>
        <Button Margin="10,0,10,0">Button 4</Button>
        <Button Margin="10,0,10,0">Button 5</Button>
        <Button Margin="10,0,10,0">Button 6</Button>
        <TextBlock HorizontalAlignment="Center">ColumnDefinition.Width="*"</TextBlock>
        <TextBlock HorizontalAlignment="Center">StackPanel.HorizontalAlignment="Stretch"</TextBlock>
        <TextBlock HorizontalAlignment="Center">StackPanel.VerticalAlignment="Top"</TextBlock>
        <TextBlock HorizontalAlignment="Center">StackPanel.Orientation="Horizontal"</TextBlock>
        <TextBlock HorizontalAlignment="Center">Button.Margin="10,0,10,0"</TextBlock>
    </StackPanel>

    <StackPanel Grid.Column="2" Grid.Row="0"
                HorizontalAlignment="Left"
                Name="StackPanel3"
                VerticalAlignment="Top">
        <TextBlock FontSize="18" HorizontalAlignment="Center" Margin="0,0,0,15">StackPanel3</TextBlock>
        <Button Margin="10">Button 7</Button>
        <Button Margin="10">Button 8</Button>
        <Button Margin="10">Button 9</Button>
        <TextBlock>ColumnDefinition.Width="Auto"</TextBlock>
        <TextBlock>StackPanel.HorizontalAlignment="Left"</TextBlock>
        <TextBlock>StackPanel.VerticalAlignment="Top"</TextBlock>
        <TextBlock>StackPanel.Orientation="Vertical"</TextBlock>
        <TextBlock>Button.Margin="10"</TextBlock>
    </StackPanel>
  </Grid>
</Border>
```

このアプリケーションをコンパイルすると、下図のようなUIが得られます。様々なプロパティ値の効果は、要素間の間隔に現れており、各列の要素の重要なプロパティ値は、`TextBlock`要素内に表示されています。

![Several positioning properties in one application](../../.gitbook/assets/layout-margins-padding-aligment-graphic3.png)
