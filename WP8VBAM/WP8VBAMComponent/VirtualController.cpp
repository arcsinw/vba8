#include "pch.h"
#include "VirtualController.h"
#include "EmulatorSettings.h"
#include <string>
#include <xstring>
#include <sstream>

using namespace std;

using namespace PhoneDirect3DXamlAppComponent;
	
#define VCONTROLLER_Y_OFFSET_WVGA				218
#define VCONTROLLER_Y_OFFSET_WXGA				348
#define VCONTROLLER_Y_OFFSET_720P				308
#define VCONTROLLER_BUTTON_Y_OFFSET_WVGA			303
#define VCONTROLLER_BUTTON_Y_OFFSET_WXGA			498
#define VCONTROLLER_BUTTON_Y_OFFSET_720P			428

namespace Emulator
{
	extern bool gbaROMLoaded;

	VirtualController *VirtualController::singleton = nullptr;

	VirtualController *VirtualController::GetInstance(void)
	{
		return singleton;
	}

	VirtualController::VirtualController(void)
		: virtualControllerOnTop(false), stickFingerDown(false)
	{
		InitializeCriticalSectionEx(&this->cs, 0, 0);
		this->pointers = ref new Platform::Collections::Map<unsigned int, PointerPoint ^>();
		this->pointerDescriptions = ref new Platform::Collections::Map<unsigned int, String^>();
		singleton = this;
	}

	VirtualController::~VirtualController(void)
	{
		DeleteCriticalSection(&this->cs);
	}
	
	int VirtualController::GetFormat(void)
	{
		return this->format;
	}
	
	void VirtualController::UpdateFormat(int format)
	{
		this->format = format;
		switch(format)
		{
		case WXGA:
			this->width = 1280;
			this->height = 768;
			this->touchWidth = 800;
			this->touchHeight = 480;

			break;
		case HD720P:
			this->width = 1280;
			this->height = 720;
			this->touchWidth = 853;
			this->touchHeight = 480;
			break;
		default:
			this->width = 800;
			this->height = 480;
			this->touchWidth = 800;
			this->touchHeight = 480;
			break;
		}
		this->hscale = ((float)this->height) / this->touchHeight;

		this->SetControllerPositionFromSettings();

		this->CreateRenderRectangles();

		if(this->orientation != ORIENTATION_PORTRAIT)
			this->CreateTouchLandscapeRectangles();
		else
			this->CreateTouchPortraitRectangles();		

	}

	void VirtualController::SetControllerPositionFromSettings(void)
	{
		EmulatorSettings ^settings = EmulatorSettings::Current;

		if(this->orientation != ORIENTATION_PORTRAIT)
		{
			padCenterX = settings->PadCenterXL;
			padCenterY = settings->PadCenterYL;
			aLeft = settings->ALeftL;
			aTop = settings->ATopL;
			bLeft = settings->BLeftL;
			bTop = settings->BTopL;
			startLeft = settings->StartLeftL;
			startTop = settings->StartTopL;
			selectRight = settings->SelectRightL;
			selectTop = settings->SelectTopL;
			lLeft = settings->LLeftL;
			lTop = settings->LTopL;
			rRight = settings->RRightL;
			rTop = settings->RTopL;
		}
		else
		{
			padCenterX = settings->PadCenterXP;
			padCenterY = settings->PadCenterYP;
			aLeft = settings->ALeftP;
			aTop = settings->ATopP;
			bLeft = settings->BLeftP;
			bTop = settings->BTopP;
			startLeft = settings->StartLeftP;
			startTop = settings->StartTopP;
			selectRight = settings->SelectRightP;
			selectTop = settings->SelectTopP;
			lLeft = settings->LLeftP;
			lTop = settings->LTopP;
			rRight = settings->RRightP;
			rTop = settings->RTopP;
		}
	}

	void VirtualController::CreateRenderRectangles(void)
	{	

		float value = EmulatorSettings::Current->ControllerScale / 100.0f;
		float value2 = EmulatorSettings::Current->ButtonScale / 100.0f;

		// Visible Rectangles
		this->padCrossRectangle.left = padCenterX - 105 * value * this->hscale ;
		this->padCrossRectangle.right =  padCenterX + 105 * value * this->hscale ;
		this->padCrossRectangle.top = padCenterY -  105 * value * this->hscale;
		this->padCrossRectangle.bottom = padCenterY +  105 * value * this->hscale;

		this->aRectangle.left = aLeft; 
		this->aRectangle.right =  this->aRectangle.left + 120 * value2 * this->hscale;
		this->aRectangle.top = aTop;
		this->aRectangle.bottom = this->aRectangle.top + 120 * value2 * this->hscale;

		this->bRectangle.left = bLeft; 
		this->bRectangle.right = this->bRectangle.left + 120 * value2 * this->hscale;
		this->bRectangle.top = bTop; 
		this->bRectangle.bottom = this->bRectangle.top + 120 * value2 * this->hscale;


		this->startRectangle.left = startLeft;
		this->startRectangle.right = this->startRectangle.left + 100 * value2 * this->hscale; 
		this->startRectangle.top = startTop; 
		this->startRectangle.bottom = this->startRectangle.top + 50 * value2 * this->hscale;

		this->selectRectangle.right = selectRight;
		this->selectRectangle.left = this->selectRectangle.right - 100 * value2 * this->hscale; 
		this->selectRectangle.top = selectTop; 
		this->selectRectangle.bottom = this->selectRectangle.top + 50 * value2 * this->hscale;



		this->lRectangle.left = lLeft;
		this->lRectangle.right = this->lRectangle.left +  90 * value2 * this->hscale;
		this->lRectangle.top =  lTop; 
		this->lRectangle.bottom = this->lRectangle.top +  53 * value2 * this->hscale;

		
		this->rRectangle.right = rRight;
		this->rRectangle.left = this->rRectangle.right - 90 * value2 * this->hscale; 
		this->rRectangle.top =  rTop; 
		this->rRectangle.bottom = this->rRectangle.top +  53 * value2 * this->hscale; 

		


		this->visibleStickPos.x = (LONG) ((this->padCrossRectangle.right + this->padCrossRectangle.left) / 2.0f);
		this->visibleStickPos.y = (LONG) ((this->padCrossRectangle.top + this->padCrossRectangle.bottom) / 2.0f);

		this->visibleStickOffset.x = 0;
		this->visibleStickOffset.y = 0;

		
	}


	void VirtualController::CreateTouchLandscapeRectangles(void)
	{
		EmulatorSettings ^settings = EmulatorSettings::Current;

		float touchVisualQuotientH = (this->height / (float) this->touchHeight);
		float touchVisualQuotientW = (this->width / (float) this->touchWidth);

		this->lRect.X = lRectangle.left / touchVisualQuotientW;
		this->lRect.Y = (this->height - lRectangle.bottom) / touchVisualQuotientH;
		this->lRect.Width = (lRectangle.right - lRectangle.left) / touchVisualQuotientW;
		this->lRect.Height = (lRectangle.bottom - lRectangle.top) / touchVisualQuotientH;
	
		this->rRect.X = rRectangle.left / touchVisualQuotientW;
		this->rRect.Y = (this->height - rRectangle.bottom) / touchVisualQuotientH;
		this->rRect.Width = (rRectangle.right - rRectangle.left) / touchVisualQuotientW;
		this->rRect.Height = (rRectangle.bottom - rRectangle.top) / touchVisualQuotientH;

		// Cross		
		this->leftRect.X = this->padCrossRectangle.left / touchVisualQuotientW;
		this->leftRect.Y = (this->height - this->padCrossRectangle.bottom) / touchVisualQuotientH;
		this->leftRect.Width = ((this->padCrossRectangle.right - this->padCrossRectangle.left) / 3.0f) / touchVisualQuotientW;
		this->leftRect.Height = (this->padCrossRectangle.bottom - this->padCrossRectangle.top) / touchVisualQuotientH;
				
		this->rightRect.Width = ((this->padCrossRectangle.right - this->padCrossRectangle.left) / 3.0f) / touchVisualQuotientW;
		this->rightRect.X = (this->padCrossRectangle.left / touchVisualQuotientW) + 2.0f * this->rightRect.Width;
		this->rightRect.Y = (this->height - this->padCrossRectangle.bottom) / touchVisualQuotientH;
		this->rightRect.Height = (this->padCrossRectangle.bottom - this->padCrossRectangle.top) / touchVisualQuotientH;
		
		this->upRect.Height = ((this->padCrossRectangle.bottom - this->padCrossRectangle.top) / 3.0f) / touchVisualQuotientH;
		this->upRect.X = (this->padCrossRectangle.left) / touchVisualQuotientW;
		this->upRect.Y = (this->height - this->padCrossRectangle.bottom) / touchVisualQuotientH + 2.0f * this->upRect.Height;
		this->upRect.Width = (this->padCrossRectangle.right - this->padCrossRectangle.left) / touchVisualQuotientW;
		
		this->downRect.Height = ((this->padCrossRectangle.bottom - this->padCrossRectangle.top) / 3.0f) / touchVisualQuotientH;
		this->downRect.X = (this->padCrossRectangle.left) / touchVisualQuotientW;
		this->downRect.Y = (this->height - this->padCrossRectangle.bottom) / touchVisualQuotientH;
		this->downRect.Width = (this->padCrossRectangle.right - this->padCrossRectangle.left) / touchVisualQuotientW;
				
		
		this->aRect.X = this->aRectangle.left / touchVisualQuotientW;
		this->aRect.Y = (this->height - this->aRectangle.bottom) / touchVisualQuotientH;
		this->aRect.Width = (this->aRectangle.right - this->aRectangle.left) / touchVisualQuotientW;
		this->aRect.Height = (this->aRectangle.bottom - this->aRectangle.top)  / touchVisualQuotientH;
			
		this->bRect.X = this->bRectangle.left / touchVisualQuotientW;
		this->bRect.Y = (this->height - this->bRectangle.bottom) / touchVisualQuotientH;
		this->bRect.Width = (this->bRectangle.right - this->bRectangle.left) / touchVisualQuotientW;
		this->bRect.Height = (this->bRectangle.bottom - this->bRectangle.top)  / touchVisualQuotientH;

		this->selectRect.X = this->selectRectangle.left / touchVisualQuotientW;
		this->selectRect.Y = (this->height - this->selectRectangle.bottom) / touchVisualQuotientH;
		this->selectRect.Width = (this->selectRectangle.right - this->selectRectangle.left) / touchVisualQuotientW;
		this->selectRect.Height = (this->selectRectangle.bottom - this->selectRectangle.top)  / touchVisualQuotientH;


		this->startRect.X = this->startRectangle.left / touchVisualQuotientW;
		this->startRect.Y = (this->height - this->startRectangle.bottom) / touchVisualQuotientH;
		this->startRect.Width = (this->startRectangle.right - this->startRectangle.left) / touchVisualQuotientW;
		this->startRect.Height = (this->startRectangle.bottom - this->startRectangle.top)  / touchVisualQuotientH;




		int dpad = settings->DPadStyle;
		if (dpad <=1)
		{
			//this->stickBoundaries.Y = 0;
			//this->stickBoundaries.X = 0;
			//this->stickBoundaries.Height = this->selectRect.X;
			//this->stickBoundaries.Width = this->lRect.Y;

			this->stickBoundaries.Y = this->leftRect.X;
			this->stickBoundaries.X = this->leftRect.Y;
			this->stickBoundaries.Width = this->leftRect.Width * 3;
			this->stickBoundaries.Height = this->leftRect.Height;
		}
		else
		{
			this->stickPos.Y = this->leftRect.X + this->leftRect.Width * 1.5f;
			this->stickPos.X = this->leftRect.Y + this->leftRect.Height / 2.0f;

			this->stickOffset.X = 0.0f;
			this->stickOffset.Y = 0.0f;

			if(dpad == 2)
			{
				this->stickBoundaries.Y = this->leftRect.X;
				this->stickBoundaries.X = this->leftRect.Y;
				this->stickBoundaries.Width = this->leftRect.Width * 3;
				this->stickBoundaries.Height = this->leftRect.Height;
			}else
			{
				this->stickBoundaries.Y = this->leftRect.X;
				this->stickBoundaries.X = this->leftRect.Y;
				this->stickBoundaries.Width = this->leftRect.Width * 3;
				this->stickBoundaries.Height = this->leftRect.Height;

				//this->stickBoundaries.Y = 0;
				//this->stickBoundaries.X = 0;
				//this->stickBoundaries.Height = this->selectRect.X;
				//this->stickBoundaries.Width = this->lRect.Y;

			}
		}
	}





	
	void VirtualController::CreateTouchPortraitRectangles(void)
	{
		EmulatorSettings ^settings = EmulatorSettings::Current;

		this->lRect.X = (this->lRectangle.top / (float)this->width) * this->touchWidth;
		this->lRect.Y = (this->lRectangle.left / (float) this->height) * this->touchHeight;
		this->lRect.Width = ((this->lRectangle.bottom - this->lRectangle.top) / (float)this->width) * this->touchWidth;
		this->lRect.Height = ((this->lRectangle.right - this->lRectangle.left) / (float)this->height) * this->touchHeight;
	
		this->rRect.X = (this->rRectangle.top / (float)this->width) * this->touchWidth;
		this->rRect.Y = (this->rRectangle.left / (float) this->height) * this->touchHeight;
		this->rRect.Width = ((this->rRectangle.bottom - this->rRectangle.top) / (float)this->width) * this->touchWidth;
		this->rRect.Height = ((this->rRectangle.right - this->rRectangle.left) / (float)this->height) * this->touchHeight;

		// Cross		
		this->leftRect.X = (this->padCrossRectangle.top / (float)this->width) * this->touchWidth;
		this->leftRect.Y = (this->padCrossRectangle.left / (float) this->height) * this->touchHeight;
		this->leftRect.Width = ((this->padCrossRectangle.bottom - this->padCrossRectangle.top) / (float)this->width) * this->touchWidth;
		this->leftRect.Height = (((this->padCrossRectangle.right - this->padCrossRectangle.left) / 3.0f) / (float)this->height) * this->touchHeight;
		
		this->rightRect.Height = (((this->padCrossRectangle.right - this->padCrossRectangle.left) / 3.0f) / (float)this->height) * this->touchHeight;
		this->rightRect.X = (this->padCrossRectangle.top / (float)this->width) * this->touchWidth;
		this->rightRect.Y = ((this->padCrossRectangle.left / (float) this->height) * this->touchHeight) + 2.0f * this->rightRect.Height;
		this->rightRect.Width = ((this->padCrossRectangle.bottom - this->padCrossRectangle.top) / (float)this->width) * this->touchWidth;

		this->upRect.X = (this->padCrossRectangle.top / (float)this->width) * this->touchWidth;
		this->upRect.Y = (this->padCrossRectangle.left / (float) this->height) * this->touchHeight;
		this->upRect.Width = (((this->padCrossRectangle.bottom - this->padCrossRectangle.top) / 3.0f) / (float)this->width) * this->touchWidth;
		this->upRect.Height = ((this->padCrossRectangle.right - this->padCrossRectangle.left) / (float)this->height) * this->touchHeight;
		
		this->downRect.Width = (((this->padCrossRectangle.bottom - this->padCrossRectangle.top) / 3.0f) / (float)this->width) * this->touchWidth;
		this->downRect.X = ((this->padCrossRectangle.top / (float)this->width) * this->touchWidth) + 2.0f * this->downRect.Width;
		this->downRect.Y = (this->padCrossRectangle.left / (float) this->height) * this->touchHeight;
		this->downRect.Height = ((this->padCrossRectangle.right - this->padCrossRectangle.left) / (float)this->height) * this->touchHeight;

		// Buttons
	
		this->aRect.X = (this->aRectangle.top / (float)this->width) * this->touchWidth;
		this->aRect.Y = (this->aRectangle.left / (float) this->height) * this->touchHeight;
		this->aRect.Width = ((this->aRectangle.bottom - this->aRectangle.top) / (float)this->width) * this->touchWidth;
		this->aRect.Height = ((this->aRectangle.right - this->aRectangle.left) / (float)this->height) * this->touchHeight;

		this->bRect.X = (this->bRectangle.top / (float)this->width) * this->touchWidth;
		this->bRect.Y = (this->bRectangle.left / (float) this->height) * this->touchHeight;
		this->bRect.Width = ((this->bRectangle.bottom - this->bRectangle.top) / (float)this->width) * this->touchWidth;
		this->bRect.Height = ((this->bRectangle.right - this->bRectangle.left) / (float)this->height) * this->touchHeight;
	
		this->selectRect.X = (this->selectRectangle.top / (float)this->width) * this->touchWidth;
		this->selectRect.Y = (this->selectRectangle.left / (float) this->height) * this->touchHeight;
		this->selectRect.Width = ((this->selectRectangle.bottom - this->selectRectangle.top) / (float)this->width) * this->touchWidth;
		this->selectRect.Height = ((this->selectRectangle.right - this->selectRectangle.left) / (float)this->height) * this->touchHeight;

		this->startRect.X = (this->startRectangle.top / (float)this->width) * this->touchWidth;
		this->startRect.Y = (this->startRectangle.left / (float) this->height) * this->touchHeight;
		this->startRect.Width = ((this->startRectangle.bottom - this->startRectangle.top) / (float)this->width) * this->touchWidth;
		this->startRect.Height = ((this->startRectangle.right - this->startRectangle.left) / (float)this->height) * this->touchHeight;

		int dpad = EmulatorSettings::Current->DPadStyle;
		if (dpad <=1)
		{
			this->stickBoundaries.Y = this->leftRect.X;
			this->stickBoundaries.X = this->leftRect.Y;
			this->stickBoundaries.Width = this->leftRect.Width;
			this->stickBoundaries.Height = this->leftRect.Height * 3;
		}
		else
		{
			this->stickPos.X = this->leftRect.Y + this->leftRect.Height * 1.5f;
			this->stickPos.Y = this->leftRect.X + this->leftRect.Width / 2.0f;

			this->stickOffset.X = 0.0f;
			this->stickOffset.Y = 0.0f;
			if(dpad == 2)
			{
				this->stickBoundaries.Y = this->leftRect.X;
				this->stickBoundaries.X = this->leftRect.Y;
				this->stickBoundaries.Width = this->leftRect.Width;
				this->stickBoundaries.Height = this->leftRect.Height * 3;
			}else
			{
				//this->stickBoundaries.Y = this->leftRect.X;
				//this->stickBoundaries.X = 0;
				//this->stickBoundaries.Height = abs(this->stickBoundaries.Y - this->lRect.X);
				//this->stickBoundaries.Width = this->bRect.Y;
				this->stickBoundaries.Y = this->leftRect.X;
				this->stickBoundaries.X = this->leftRect.Y;
				this->stickBoundaries.Width = this->leftRect.Width;
				this->stickBoundaries.Height = this->leftRect.Height * 3;
			}
		}
	}

	void VirtualController::VirtualControllerOnTop(bool onTop)
	{
		this->virtualControllerOnTop = onTop;
		this->UpdateFormat(this->format);
	}

	void VirtualController::SetOrientation(int orientation)
	{
		this->orientation = orientation;
		this->UpdateFormat(this->format);
	}

	void VirtualController::PointerPressed(PointerPoint ^point)
	{
		EnterCriticalSection(&this->cs);
		this->pointers->Insert(point->PointerId, point);
		this->pointerDescriptions->Insert(point->PointerId, "");
		

		int dpad = EmulatorSettings::Current->DPadStyle;
		if(dpad >= 2)
		{
			Windows::Foundation::Point p = point->Position;
			if(this->orientation == ORIENTATION_LANDSCAPE_RIGHT)
			{
				p.X = this->touchHeight - p.X;
				p.Y = this->touchWidth - p.Y;
			}

			if(this->stickBoundaries.Contains(p) && !stickFingerDown)
			{
				float scale = (int) Windows::Graphics::Display::DisplayProperties::ResolutionScale / 100.0f;
				if(dpad == 3)
				{
					stickPos = p;

					if(this->orientation != ORIENTATION_PORTRAIT)
					{
						this->visibleStickPos.x = this->stickPos.Y * scale;
						this->visibleStickPos.y = this->height - this->stickPos.X * scale;
					}else
					{
						this->visibleStickPos.x = this->stickPos.X * scale;
						this->visibleStickPos.y = this->stickPos.Y * scale;
					}
				}

				stickFingerID = point->PointerId;
				stickFingerDown = true;

				stickOffset.X = p.X - this->stickPos.X;
				stickOffset.Y = p.Y - this->stickPos.Y;

				this->visibleStickOffset.x = this->stickOffset.X * scale;
				this->visibleStickOffset.y = this->stickOffset.Y * scale;
			}
		}

		LeaveCriticalSection(&this->cs);
		
	}

	void VirtualController::PointerMoved(PointerPoint ^point)
	{
		EnterCriticalSection(&this->cs);
		if(this->pointers->HasKey(point->PointerId))
		{
			this->pointers->Insert(point->PointerId, point);
		}
		

		int dpad = EmulatorSettings::Current->DPadStyle;
		if(dpad >= 2)
		{
			if(this->stickFingerDown && point->PointerId == this->stickFingerID)
			{
				Windows::Foundation::Point p = point->Position;
				if(this->orientation == ORIENTATION_LANDSCAPE_RIGHT)
				{
					p.X = this->touchHeight - p.X;
					p.Y = this->touchWidth - p.Y;
				}
				float scale = (int) Windows::Graphics::Display::DisplayProperties::ResolutionScale / 100.0f;

				stickOffset.X = p.X - this->stickPos.X;
				stickOffset.Y = p.Y - this->stickPos.Y;

				this->visibleStickOffset.x = this->stickOffset.X * scale;
				this->visibleStickOffset.y = this->stickOffset.Y * scale;
			}
		}

		LeaveCriticalSection(&this->cs);
	}

	void VirtualController::PointerReleased(PointerPoint ^point)
	{
		EnterCriticalSection(&this->cs);
		if(this->pointers->HasKey(point->PointerId))
		{
			//get the description
			String^ desc = pointerDescriptions->Lookup(point->PointerId);
			unsigned int key2 = point->PointerId;

			this->pointers->Remove(point->PointerId);
			this->pointerDescriptions->Remove(point->PointerId);

			//find point that may not be removed due to released event not triggered and mark it
			if (desc != "")
			{
				for (auto i = this->pointerDescriptions->First(); i->HasCurrent; i->MoveNext())
				{
					String ^desc2= i->Current->Value;
					unsigned int key2 = i->Current->Key;


					if (desc2 == desc ) 
					{
						//remove the points
						this->pointerDescriptions->Remove(key2);
						this->pointers->Remove(key2);
						break; //has to break or the loop will cause Changed_state exception
					}

				}
			}
			

			int dpad = EmulatorSettings::Current->DPadStyle;
			if(dpad >= 2)
			{
				if(this->stickFingerDown && desc == "joystick")
				{
					this->stickFingerDown = false;
					this->stickFingerID = 0;

					this->stickOffset.X = 0;
					this->stickOffset.Y = 0;

					this->visibleStickOffset.x = 0;
					this->visibleStickOffset.y = 0;
				}
			}
		}

		

		LeaveCriticalSection(&this->cs);
	}
	
	const ControllerState *VirtualController::GetControllerState(void)
	{
		ZeroMemory(&this->state, sizeof(ControllerState));

		int dpad = EmulatorSettings::Current->DPadStyle;

		EnterCriticalSection(&this->cs);
		for (auto i = this->pointers->First(); i->HasCurrent; i->MoveNext())
		{
			PointerPoint ^p = i->Current->Value;
			Windows::Foundation::Point point = Windows::Foundation::Point(p->Position.Y, p->Position.X);
			if(this->orientation == ORIENTATION_LANDSCAPE_RIGHT)
			{
				point.X = this->touchWidth - point.X;
				point.Y = this->touchHeight - point.Y;
			}

			if(dpad == 0 || dpad == 1)
			{
				if(this->leftRect.Contains(point))
				{
					state.LeftPressed = true;
					//add the description for this point
					this->pointerDescriptions->Insert(i->Current->Key, "joystick");
				}
				if(this->upRect.Contains(point))
				{
					state.UpPressed = true;
					this->pointerDescriptions->Insert(i->Current->Key, "joystick");
				}
				if(this->rightRect.Contains(point))
				{
					state.RightPressed = true;
					this->pointerDescriptions->Insert(i->Current->Key, "joystick");
				}
				if(this->downRect.Contains(point))
				{
					state.DownPressed = true;
					this->pointerDescriptions->Insert(i->Current->Key, "joystick");
				}

				if (dpad == 0)
				{
					//this code make the d-pad a 4-way only, i.e. resolve conflict
					if (state.LeftPressed && state.UpPressed)
					{
						Windows::Foundation::Point pointUp = Windows::Foundation::Point(this->upRect.X, this->upRect.Y + upRect.Height);
						Windows::Foundation::Point pointLeft = Windows::Foundation::Point(this->leftRect.X + this->leftRect.Width, this->leftRect.Y);
						if (CalculateDistanceDiff(pointUp, pointLeft, point) < 0)
							state.LeftPressed = false;
						else
							state.UpPressed = false;

					}
					else if (state.LeftPressed && state.DownPressed)
					{
					
						Windows::Foundation::Point pointLeft = Windows::Foundation::Point(leftRect.X, leftRect.Y);
						Windows::Foundation::Point pointDown = Windows::Foundation::Point(downRect.X + downRect.Width, downRect.Y + downRect.Height);
						if (CalculateDistanceDiff(pointLeft, pointDown, point) < 0)
							state.DownPressed = false;
						else
							state.LeftPressed = false;

					}
					else if (state.RightPressed && state.DownPressed)
					{
					
						Windows::Foundation::Point pointRight = Windows::Foundation::Point(rightRect.X, rightRect.Y + rightRect.Height);
						Windows::Foundation::Point pointDown = Windows::Foundation::Point(downRect.X + downRect.Width, downRect.Y);
						if (CalculateDistanceDiff(pointRight, pointDown, point) < 0)
							state.DownPressed = false;
						else
							state.RightPressed = false;

					}
					else if (state.RightPressed && state.UpPressed)
					{
					
						Windows::Foundation::Point pointRight = Windows::Foundation::Point(rightRect.X + rightRect.Width, rightRect.Y + rightRect.Height);
						Windows::Foundation::Point pointUp = Windows::Foundation::Point(upRect.X , upRect.Y);
						if (CalculateDistanceDiff(pointRight, pointUp, point) < 0)
							state.UpPressed = false;
						else
							state.RightPressed = false;

					}
				}

			}else
			{
				if (this->stickBoundaries.Contains(p->Position))
					this->pointerDescriptions->Insert(i->Current->Key, "joystick");

				if(this->stickFingerDown && p->PointerId == this->stickFingerID)
				{
					float deadzone = EmulatorSettings::Current->Deadzone;
					float controllerScale = EmulatorSettings::Current->ControllerScale / 100.0f;
					float length = (float) sqrt(this->stickOffset.X * this->stickOffset.X + this->stickOffset.Y * this->stickOffset.Y);					
					float scale = (int) Windows::Graphics::Display::DisplayProperties::ResolutionScale / 100.0f;
					if(length >= deadzone * scale * controllerScale)
					{
						// Deadzone of 15
						float unitX = 1.0f;
						float unitY = 0.0f;
						float normX = this->stickOffset.X / length;
						float normY = this->stickOffset.Y / length;

						float dot = unitX * normX + unitY * normY;
						float rad = (float) acos(dot);

						if(normY > 0.0f)
						{
							rad = 6.28f - rad;
						}

						if(this->orientation != ORIENTATION_PORTRAIT)
						{
							rad = (rad + 3.14f / 2.0f);
							if(rad > 6.28f)
							{
								rad -= 6.28f;
							}
						}

						if((rad >= 0 && rad < 1.046f) || (rad > 5.234f && rad < 6.28f))
						{
							state.RightPressed = true;
							
						}
						if(rad >= 0.523f && rad < 2.626f)
						{
							state.UpPressed = true;
							
						}
						if(rad >= 2.093f && rad < 4.186f)
						{
							state.LeftPressed = true;
							
						}
						if(rad >= 3.663f && rad < 5.756f)
						{
							state.DownPressed = true;
							
						}
					}
				}
			}
			if(this->startRect.Contains(point))
			{
				state.StartPressed = true;
				this->pointerDescriptions->Insert(i->Current->Key, "start");
			}
			if(this->selectRect.Contains(point))
			{
				state.SelectPressed = true;
				this->pointerDescriptions->Insert(i->Current->Key, "select");
			}
			if(this->lRect.Contains(point))
			{
				state.LPressed = true;
				this->pointerDescriptions->Insert(i->Current->Key, "l");
			}
			if(this->rRect.Contains(point))
			{
				state.RPressed = true;
				this->pointerDescriptions->Insert(i->Current->Key, "r");
			}
			if(this->aRect.Contains(point))
			{
				state.APressed = true;
				this->pointerDescriptions->Insert(i->Current->Key, "a");
			}
			if(this->bRect.Contains(point))
			{
				state.BPressed = true;
				this->pointerDescriptions->Insert(i->Current->Key, "b");
			}
		}
		LeaveCriticalSection(&this->cs);

		return &this->state;
	}

	double VirtualController::CalculateDistanceDiff(Windows::Foundation::Point point1, Windows::Foundation::Point point2, Windows::Foundation::Point target)
	{
		double distance1 = sqrt(pow(point1.X - target.X, 2.0) + pow(point1.Y - target.Y, 2.0));
		double distance2 = sqrt(pow(point2.X - target.X, 2.0) + pow(point2.Y - target.Y, 2.0));
		return distance1 - distance2;
	}

	void VirtualController::GetCrossRectangle(RECT *rect)
	{
		*rect = this->padCrossRectangle;
	}

	void VirtualController::GetARectangle(RECT *rect)
	{
		*rect = this->aRectangle;
	}

	void VirtualController::GetBRectangle(RECT *rect)
	{
		*rect = this->bRectangle;
	}


	void VirtualController::GetStartRectangle(RECT *rect)
	{
		*rect = this->startRectangle;
	}

	void VirtualController::GetSelectRectangle(RECT *rect)
	{
		*rect = this->selectRectangle;
	}


	void VirtualController::GetLRectangle(RECT *rect)
	{
		*rect = this->lRectangle;
	}

	void VirtualController::GetRRectangle(RECT *rect)
	{
		*rect = this->rRectangle;
	}
	
	void VirtualController::GetStickRectangle(RECT *rect)
	{
		int quarterWidth = (this->padCrossRectangle.right - this->padCrossRectangle.left) / 5;
		int quarterHeight = (this->padCrossRectangle.bottom - this->padCrossRectangle.top) / 5;

		if(this->orientation != ORIENTATION_PORTRAIT)
		{
			rect->left = (this->visibleStickPos.x + this->visibleStickOffset.y) - quarterWidth;
			rect->right = rect->left + 2 * quarterWidth;
			rect->top = (this->visibleStickPos.y - this->visibleStickOffset.x) - quarterHeight;
			rect->bottom = rect->top + 2 * quarterHeight;
		}else
		{

			rect->left = (this->visibleStickPos.x + this->visibleStickOffset.x) - quarterWidth;
			rect->right = rect->left + 2 * quarterWidth;
			rect->top = (this->visibleStickPos.y + this->visibleStickOffset.y) - quarterHeight;
			rect->bottom = rect->top + 2 * quarterHeight;
		}
	}
	
	void VirtualController::GetStickCenterRectangle(RECT *rect)
	{
		int quarterWidth = (this->padCrossRectangle.right - this->padCrossRectangle.left) / 20;
		int quarterHeight = (this->padCrossRectangle.bottom - this->padCrossRectangle.top) / 20;

		if(this->orientation != ORIENTATION_PORTRAIT)
		{
			rect->left = this->visibleStickPos.x - quarterWidth;
			rect->right = rect->left + 2 * quarterWidth;
			rect->top = this->visibleStickPos.y - quarterHeight;
			rect->bottom = rect->top + 2 * quarterHeight;
		}else
		{

			rect->left = this->visibleStickPos.x - quarterWidth;
			rect->right = rect->left + 2 * quarterWidth;
			rect->top = this->visibleStickPos.y - quarterHeight;
			rect->bottom = rect->top + 2 * quarterHeight;
		}
	}

	bool VirtualController::StickFingerDown(void)
	{
		return this->stickFingerDown;
	}
}