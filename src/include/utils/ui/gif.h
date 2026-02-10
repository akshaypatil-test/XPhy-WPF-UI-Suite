#pragma once

#include "desktop/framework.h"
#define NOMINMAX
#include<windows.h>

using namespace Gdiplus;
namespace edf::utils::ImageUtilsUi {
struct gifStruct {
    Image* m_pImage;
    GUID* m_pDimensionIDs;
    UINT m_FrameCount;
    PropertyItem* m_pItem;
    UINT m_iCurrentFrame;
    BOOL m_bIsPlaying;
    BOOL isPlayable;
    UINT gifResourceId;
    UINT gifAnimationTimerResId;
    int posX;
    int posY;
    int width;
    int height;
};
RECT getGifRect(gifStruct& gs);
Bitmap* LoadImageFromResource(HMODULE hMod, const wchar_t* resid, const wchar_t* restype);
Image* LoadImageFromResource(HMODULE hMod, const HINSTANCE& hInst, const wchar_t* resid, const wchar_t* restype);
void LoadGifImage(const HINSTANCE& hInst, gifStruct& gs);
void OnTimer(HWND mainHwnd, gifStruct& gs);
void AnimateGIF(HWND mainHwnd, gifStruct& gs);
void DrawItem(const HDC& hdc, HWND mainHwnd, gifStruct& gs);
void Destroy(gifStruct& gs);
} // namespace edf::utils::ImageUtilsUi
