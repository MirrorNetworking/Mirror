# Simulation Timer
An Lightweight Efficient Timer for Unity. Inspired by Photon Fusion TickTimer
## Usage/Examples

#### Simulation Timer

![](https://github.com/StinkySteak/com.stinkysteak.simulationtimer/blob/main/Gif/DefaultTimer.gif)

```csharp
private SimulationTimer _disableTimer;

private void Start()
{
    _disableTimer = SimulationTimer.CreateFromSeconds(_delay);
}

private void Update()
{
    if(_disableTimer.IsExpired())
    {
        _gameObject.SetActive(false);
        _disableTimer = SimulationTimer.None;
    }
}
```

#### Pauseable Simulation Timer

![](https://github.com/StinkySteak/com.stinkysteak.simulationtimer/blob/main/Gif/PauseableTimer.gif)

```csharp
private PauseableSimulationTimer _timer;

public PauseableSimulationTimer Timer => _timer;

private void Start()
{
    _timer = PauseableSimulationTimer.CreateFromSeconds(_delay);
}

public void TogglePause()
{
    if(!_timer.IsPaused)
    {
        _timer.Pause();
        return;
    }

    _timer.Resume();
}

private void Update()
{
    if(_timer.IsExpired())
    {
        _gameObject.SetActive(false);
        _timer = PauseableSimulationTimer.None;
    }
}
```
## Class Reference
`SimulationTimer`: Default Timer    
`PauseableTimer`: Pauseable Timer
