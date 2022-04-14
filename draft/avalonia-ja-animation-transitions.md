# Transitions

Avaloniaのトランジションは、CSSのアニメーションに大きく影響されています。これらは、ターゲットプロパティの値の変化を聞き、その後、そのパラメータに従って変化をアニメーション化します。トランジションは `Transitions` プロパティを通じて、任意の `Control` に定義することができます。

```markup
<Window xmlns="https://github.com/avaloniaui">
    <Window.Styles>
        <Style Selector="Rectangle.red">
            <Setter Property="Height" Value="100"/>
            <Setter Property="Width" Value="100"/>
            <Setter Property="Fill" Value="Red"/>
            <Setter Property="Opacity" Value="0.5"/>
        </Style>
        <Style Selector="Rectangle.red:pointerover">
            <Setter Property="Opacity" Value="1"/>
        </Style>
    </Window.Styles>

    <Rectangle Classes="red">
        <Rectangle.Transitions>
            <Transitions>
                <DoubleTransition Property="Opacity" Duration="0:0:0.2"/>
            </Transitions>
        </Rectangle.Transitions>
    </Rectangle>

</Window>
```

上記の例では、`Rectangle` の `Opacity` プロパティの変化をリスニングし、値が変化したら、古い値から新しい値へ2秒かけて滑らかに遷移させます。

トランジションは、`Transitions` をターゲットプロパティとする `Setter` を使用して、`Transitions` オブジェクトにカプセル化することで、任意のスタイルで定義することも可能です。

```markup
<Window xmlns="https://github.com/avaloniaui">
    <Window.Styles>
        <Style Selector="Rectangle.red">
            <Setter Property="Height" Value="100"/>
            <Setter Property="Width" Value="100"/>
            <Setter Property="Fill" Value="Red"/>
            <Setter Property="Opacity" Value="0.5"/>
            <Setter Property="Transitions">
                <Transitions>
                    <DoubleTransition Property="Opacity" Duration="0:0:0.2"/>
                </Transitions>
            </Setter>
        </Style>
        <Style Selector="Rectangle.red:pointerover">
            <Setter Property="Opacity" Value="1"/>
        </Style>
    </Window.Styles>

    <Rectangle Classes="red"/>

</Window>
```

すべてのトランジションは `Property`、`Delay`、`Duration` とオプションの `Easing` プロパティを持っています。

`Property` はトランジションのターゲットで、値をリッスンしたりアニメートしたりするためのものです。

`Delay` はトランジションがターゲットに適用されるまでの時間を指します。

`Duration` はトランジションが再生される時間を指します。

イージング機能は、[キーフレームアニメーション](https://docs.avaloniaui.net/docs/animations/keyframe-animations#easings)で説明したものと同じです。

トランジションには以下の種類があります。アニメートされるプロパティのタイプに応じて、正しいタイプを使用する必要があります。

* `DoubleTransitions`: For `double` target properties
* `FloatTransitions`: For `float` target properties
* `IntegerTransitions`: For `int` target properties
* `TransformOperationsTransition` : For `ITransform` properties

## Transitioning Render Transforms

CSSライクな構文でコントロールに適用されるレンダリング変換は、トランジションさせることができる。次の例は、ポインターを合わせると 45 度回転する Border を示しています。

{% tabs %}
{% tab title="XAML" %}
```markup
<Border Width="100" Height="100" Background="Red">
    <Border.Styles>
        <Style Selector="Border">
            <Setter Property="RenderTransform" Value="rotate(0)"/>
        </Style>
        <Style Selector="Border:pointerover">
            <Setter Property="RenderTransform" Value="rotate(45deg)"/>
        </Style>
    </Border.Styles>
    <Border.Transitions>
        <Transitions>
            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:1"/>
        </Transitions>
    </Border.Transitions>
</Border>
```
{% endtab %}

{% tab title="C\#" %}
```text
new Border
{
    Width = 100,
    Height = 100,
    Background = Brushes.Red,
    Styles =
    {
        new Style(x => x.OfType<Border>())
        {
            Setters =
            {
                new Setter(
                    Border.RenderTransformProperty,
                    TransformOperations.Parse("rotate(0)"))
            },
        },
        new Style(x => x.OfType<Border>().Class(":pointerover"))
        {
            Setters =
            {
                new Setter(
                    Border.RenderTransformProperty,
                    TransformOperations.Parse("rotate(45deg)"))
            },
        },
    },
    Transitions = new Transitions
    {
        new TransformOperationsTransition
        {
            Property = Border.RenderTransformProperty,
            Duration = TimeSpan.FromSeconds(1),
        }
    }
};
```
{% endtab %}
{% endtabs %}

{% hint style="info" %}
Avaloniaは、`RotateTransform` ,`ScaleTransform` などのWPFスタイルのレンダリングトランスフォームをサポートしています。これらのトランスフォームはトランジションできません。レンダリングトランスフォームにトランジションを適用する場合は、常に CSS ライクな形式を使用してください。
{% endhint %}
