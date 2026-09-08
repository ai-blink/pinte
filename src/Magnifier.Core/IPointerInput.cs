namespace Magnifier.Core;

public interface IPointerInput
{
    void MoveTo(ScreenPoint point);

    void LeftButtonDown();

    void LeftButtonUp();
}
