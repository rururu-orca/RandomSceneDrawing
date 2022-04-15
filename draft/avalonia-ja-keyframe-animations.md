# Keyframe Animations

Avaloniaのキーフレームアニメーションは、CSSアニメーションに大きく影響を受けています。キーフレームを使用して、コントロール上の任意の数のプロパティをアニメーション化し、各プロパティが通過しなければならない状態を定義することができます。キーフレーム・アニメーションは、任意の回数、任意の方向に実行することができます。

## Defining A Keyframe Animation <a id="defining-a-keyframe-animation"></a>

Keyframe animations are applied using styles. They can be defined on any style by adding an `Animation` object to the `Style.Animation` property:

```markup
<Window xmlns="https://github.com/avaloniaui">
    <Window.Styles>
        <Style Selector="Rectangle.red">
            <Setter Property="Height" Value="100"/>
            <Setter Property="Width" Value="100"/>
            <Setter Property="Fill" Value="Red"/>
            <Style.Animations>
                <Animation Duration="0:0:1"> 
                    <KeyFrame Cue="0%">
                        <Setter Property="Opacity" Value="0.0"/>
                    </KeyFrame>
                    <KeyFrame Cue="100%">
                        <Setter Property="Opacity" Value="1.0"/>
                    </KeyFrame>
                </Animation>
            </Style.Animations>
        </Style>
    </Window.Styles>

    <Rectangle Classes="red"/>
</Window>
```

上の例では、対象の `Control` をその [selector](https://docs.avaloniaui.net/docs/styling/selectors) で定義されたとおりにアニメーション化します。これは、コントロールがロードされると直ちに実行されます。

## Triggering Animations <a id="triggering-animations"></a>

WPF の `Triggers` とは異なり、XAML で定義されたアニメーションは、トリガー動作のために [セレクタ](https://docs.avaloniaui.net/docs/styling/selectors) に依存します。セレクタは常にコントロールに適用されるか、条件付きで適用されます(例: コントロールにスタイルクラスappledがある場合)。

セレクタが条件付きでない場合は、一致する `Control` がビジュアルツリーに生成されたときにアニ メーションがトリガーされます。そうでない場合は、セレクタがアクティブになるたびにアニメーションが実行されます。セレクタがマッチしなくなると、現在実行中のアニメーションはキャンセルされます。

## `KeyFrames` <a id="keyframes"></a>

`KeyFrame` オブジェクトは、ターゲットとなる `Setter` オブジェクトが、いつターゲットの `Control` に適用されるかを定義し、その間には値の補間が行われます。

`KeyFrame` オブジェクトの `Cue` プロパティは親アニメーションの `Duration` に基づいており、絶対時間インデックス \(i.e., `"0:0:1"`\) またはアニメーションの `Duration` に対するパーセント \(i.e.,  `"0%"`, `"100%"`\) が使用可能です。ただし、`Cue`の値は指定した`Duration`を超えないようにします。

すべての `Animation` オブジェクトは少なくとも一つの `KeyFrame` を含み、ターゲットプロパティと値を持つ `Setter` を持つ必要があります。

複数のプロパティを一つのアニメーションにすることも可能で、その場合は `KeyFrame` の上にさらに `Setter` オブジェクトを追加します。

```markup
<Animation Duration="0:0:1"> 
    <KeyFrame Cue="0%">
        <Setter Property="Opacity" Value="0.0"/>
        <Setter Property="RotateTransform.Angle" Value="0.0"/>
    </KeyFrame>
    <KeyFrame Cue="100%">
        <Setter Property="Opacity" Value="1.0"/>
        <Setter Property="RotateTransform.Angle" Value="90.0"/>
    </KeyFrame>
</Animation>
```

## Delay <a id="delay"></a>

アニメーションの `Delay` プロパティに必要な遅延時間を定義することで、アニメーションに遅延を追加することができます。

```markup
<Animation Duration="0:0:1"
           Delay="0:0:1"> 
    ...
</Animation>
```

## Repeat <a id="repeat"></a>

`Animation`の `IterationCount` プロパティには、以下のような繰り返し動作を設定することができます。

| Value | Description |
| :--- | :--- |
| `0` to N | Play N times. |
| `INFINITE` | Repeat Indefinitely |

## Playback Direction <a id="playback-direction"></a>

`PlaybackDirection` プロパティは、アニメーションをどのように再生するか（リピートを含む）を定義します。

The following table describes the possible behaviors:

| Value | Description |
| :--- | :--- |
| `Normal` | The animation is played normally. |
| `Reverse` | The animation is played in reverse direction. |
| `Alternate` | The animation is played forwards first, then backwards. |
| `AlternateReverse` | The animation is played backwards first, then forwards. |

## Value fill modes <a id="value-fill-modes"></a>

`FillMode` プロパティは、アニメーションの最初と最後の補間値が、アニメーションの実行前と実行後、および実行間のディレイで持続するかどうかを定義します。

次の表は、可能な動作について説明しています。

| Value | Description |
| :--- | :--- |
| `None` | Value will not persist after animation nor the first value will be applied when the animation is delayed. |
| `Forward` | The last interpolated value will be persisted to the target property. |
| `Backward` | The first interpolated value will be displayed on animation delay. |
| `Both` | Both `Forward` and `Backward` behaviors will be applied. |

## Easings <a id="easings"></a>

イージング機能は、`Animation`の `Easing` プロパティに任意の関数名を設定することで設定することができます。

```markup
<Animation Duration="0:0:1"
           Delay="0:0:1"
           Easing="BounceEaseIn"> 
    ...
</Animation>
```

また、このように独自のイージング機能クラスを追加することも可能です。

```markup
<Animation Duration="0:0:1"
           Delay="0:0:1">
    <Animation.Easing>
        <local:YourCustomEasingClassHere/>
    </Animation.Easing> 
    ...
</Animation>
```

以下のリストは、内蔵のイージング機能です。

* LinearEasing \(Default\)
* BackEaseIn
* BackEaseInOut
* BackEaseOut
* BounceEaseIn
* BounceEaseInOut
* BounceEaseOut
* CircularEaseIn
* CircularEaseInOut
* CircularEaseOut
* CubicEaseIn
* CubicEaseInOut
* CubicEaseOut
* ElasticEaseIn
* ElasticEaseInOut
* ElasticEaseOut
* ExponentialEaseIn
* ExponentialEaseInOut
* ExponentialEaseOut
* QuadraticEaseIn
* QuadraticEaseInOut
* QuadraticEaseOut
* QuarticEaseIn
* QuarticEaseInOut
* QuarticEaseOut
* QuinticEaseIn
* QuinticEaseInOut
* QuinticEaseOut
* SineEaseIn
* SineEaseInOut
* SineEaseOut
