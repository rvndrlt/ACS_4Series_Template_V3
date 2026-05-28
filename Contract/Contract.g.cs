using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Ch5_Sample_Contract
{
    /// <summary>
    /// Common Interface for Root Contracts.
    /// </summary>
    public interface IContract
    {
        object UserObject { get; set; }
        void AddDevice(BasicTriListWithSmartObject device);
        void RemoveDevice(BasicTriListWithSmartObject device);
    }

    /// <summary>
    /// ACS
    /// </summary>
    public class Contract : IContract, IDisposable
    {
        #region Components

        private ComponentMediator ComponentMediator { get; set; }

        public Ch5_Sample_Contract.Subsystem.ISubsystemList SubsystemList { get { return (Ch5_Sample_Contract.Subsystem.ISubsystemList)InternalSubsystemList; } }
        private Ch5_Sample_Contract.Subsystem.SubsystemList InternalSubsystemList { get; set; }

        public Ch5_Sample_Contract.Subsystem.ISubsystemButton[] SubsystemButton { get { return InternalSubsystemButton.Cast<Ch5_Sample_Contract.Subsystem.ISubsystemButton>().ToArray(); } }
        private Ch5_Sample_Contract.Subsystem.SubsystemButton[] InternalSubsystemButton { get; set; }

        public Ch5_Sample_Contract.videoSources.IvideoSource[] vsrcButton { get { return InternalvsrcButton.Cast<Ch5_Sample_Contract.videoSources.IvideoSource>().ToArray(); } }
        private Ch5_Sample_Contract.videoSources.videoSource[] InternalvsrcButton { get; set; }

        public Ch5_Sample_Contract.videoSources.IvideoSourceList vsrcList { get { return (Ch5_Sample_Contract.videoSources.IvideoSourceList)InternalvsrcList; } }
        private Ch5_Sample_Contract.videoSources.videoSourceList InternalvsrcList { get; set; }

        public Ch5_Sample_Contract.musicSources.ImusicSource[] musicSourceSelect { get { return InternalmusicSourceSelect.Cast<Ch5_Sample_Contract.musicSources.ImusicSource>().ToArray(); } }
        private Ch5_Sample_Contract.musicSources.musicSource[] InternalmusicSourceSelect { get; set; }

        public Ch5_Sample_Contract.musicSources.ImusicSourceList musicSourceList { get { return (Ch5_Sample_Contract.musicSources.ImusicSourceList)InternalmusicSourceList; } }
        private Ch5_Sample_Contract.musicSources.musicSourceList InternalmusicSourceList { get; set; }

        public Ch5_Sample_Contract.TabButton.ITabButton[] TabButton { get { return InternalTabButton.Cast<Ch5_Sample_Contract.TabButton.ITabButton>().ToArray(); } }
        private Ch5_Sample_Contract.TabButton.TabButton[] InternalTabButton { get; set; }

        public Ch5_Sample_Contract.Floors.IFloorSelect[] FloorSelect { get { return InternalFloorSelect.Cast<Ch5_Sample_Contract.Floors.IFloorSelect>().ToArray(); } }
        private Ch5_Sample_Contract.Floors.FloorSelect[] InternalFloorSelect { get; set; }

        public Ch5_Sample_Contract.Floors.IFloorList FloorList { get { return (Ch5_Sample_Contract.Floors.IFloorList)InternalFloorList; } }
        private Ch5_Sample_Contract.Floors.FloorList InternalFloorList { get; set; }

        public Ch5_Sample_Contract.WholeHouseZone.IWholeHouseZoneList WholeHouseZoneList { get { return (Ch5_Sample_Contract.WholeHouseZone.IWholeHouseZoneList)InternalWholeHouseZoneList; } }
        private Ch5_Sample_Contract.WholeHouseZone.WholeHouseZoneList InternalWholeHouseZoneList { get; set; }

        public Ch5_Sample_Contract.WholeHouseZone.IWholeHouseZone[] WholeHouseZone { get { return InternalWholeHouseZone.Cast<Ch5_Sample_Contract.WholeHouseZone.IWholeHouseZone>().ToArray(); } }
        private Ch5_Sample_Contract.WholeHouseZone.WholeHouseZone[] InternalWholeHouseZone { get; set; }

        public Ch5_Sample_Contract.WholeHouseSubsystem.IWholeHouseSubsystemList WholeHouseSubsystemList { get { return (Ch5_Sample_Contract.WholeHouseSubsystem.IWholeHouseSubsystemList)InternalWholeHouseSubsystemList; } }
        private Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystemList InternalWholeHouseSubsystemList { get; set; }

        public Ch5_Sample_Contract.WholeHouseSubsystem.IWholeHouseSubsystem[] WholeHouseSubsystem { get { return InternalWholeHouseSubsystem.Cast<Ch5_Sample_Contract.WholeHouseSubsystem.IWholeHouseSubsystem>().ToArray(); } }
        private Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystem[] InternalWholeHouseSubsystem { get; set; }

        public Ch5_Sample_Contract.Lights.ILightButtonList LightButtonList { get { return (Ch5_Sample_Contract.Lights.ILightButtonList)InternalLightButtonList; } }
        private Ch5_Sample_Contract.Lights.LightButtonList InternalLightButtonList { get; set; }

        public Ch5_Sample_Contract.Lights.ILightButton[] LightButton { get { return InternalLightButton.Cast<Ch5_Sample_Contract.Lights.ILightButton>().ToArray(); } }
        private Ch5_Sample_Contract.Lights.LightButton[] InternalLightButton { get; set; }

        public Ch5_Sample_Contract.SecurityBypassList.INumberOfSecurityZones NumberOfSecurityZones { get { return (Ch5_Sample_Contract.SecurityBypassList.INumberOfSecurityZones)InternalNumberOfSecurityZones; } }
        private Ch5_Sample_Contract.SecurityBypassList.NumberOfSecurityZones InternalNumberOfSecurityZones { get; set; }

        public Ch5_Sample_Contract.SecurityBypassList.ISecurityZone[] SecurityZone { get { return InternalSecurityZone.Cast<Ch5_Sample_Contract.SecurityBypassList.ISecurityZone>().ToArray(); } }
        private Ch5_Sample_Contract.SecurityBypassList.SecurityZone[] InternalSecurityZone { get; set; }

        public Ch5_Sample_Contract.Shades.IShadesList ShadesList { get { return (Ch5_Sample_Contract.Shades.IShadesList)InternalShadesList; } }
        private Ch5_Sample_Contract.Shades.ShadesList InternalShadesList { get; set; }

        public Ch5_Sample_Contract.Shades.IShadeButtons[] ShadeButtons { get { return InternalShadeButtons.Cast<Ch5_Sample_Contract.Shades.IShadeButtons>().ToArray(); } }
        private Ch5_Sample_Contract.Shades.ShadeButtons[] InternalShadeButtons { get; set; }

        public Ch5_Sample_Contract.HomePageMusicControl.IHomeMusicZone[] HomeMusicZone { get { return InternalHomeMusicZone.Cast<Ch5_Sample_Contract.HomePageMusicControl.IHomeMusicZone>().ToArray(); } }
        private Ch5_Sample_Contract.HomePageMusicControl.HomeMusicZone[] InternalHomeMusicZone { get; set; }

        public Ch5_Sample_Contract.HomePageMusicControl.IHomeNumberOfMusicZones HomeNumberOfMusicZones { get { return (Ch5_Sample_Contract.HomePageMusicControl.IHomeNumberOfMusicZones)InternalHomeNumberOfMusicZones; } }
        private Ch5_Sample_Contract.HomePageMusicControl.HomeNumberOfMusicZones InternalHomeNumberOfMusicZones { get; set; }

        public Ch5_Sample_Contract.MediaPlayer.IMediaPlayerObject MediaPlayerObject { get { return (Ch5_Sample_Contract.MediaPlayer.IMediaPlayerObject)InternalMediaPlayerObject; } }
        private Ch5_Sample_Contract.MediaPlayer.MediaPlayerObject InternalMediaPlayerObject { get; set; }

        public Ch5_Sample_Contract.MusicControl.ImusicNumberOfRooms musicNumberOfRooms { get { return (Ch5_Sample_Contract.MusicControl.ImusicNumberOfRooms)InternalmusicNumberOfRooms; } }
        private Ch5_Sample_Contract.MusicControl.musicNumberOfRooms InternalmusicNumberOfRooms { get; set; }

        public Ch5_Sample_Contract.MusicControl.IMusicRoomControl[] MusicRoomControl { get { return InternalMusicRoomControl.Cast<Ch5_Sample_Contract.MusicControl.IMusicRoomControl>().ToArray(); } }
        private Ch5_Sample_Contract.MusicControl.MusicRoomControl[] InternalMusicRoomControl { get; set; }

        public Ch5_Sample_Contract.RoomSelect.IRoom[] roomButton { get { return InternalRoom.Cast<Ch5_Sample_Contract.RoomSelect.IRoom>().ToArray(); } }
        private Ch5_Sample_Contract.RoomSelect.Room[] InternalRoom { get; set; }

        public Ch5_Sample_Contract.RoomSelect.IroomList roomList { get { return (Ch5_Sample_Contract.RoomSelect.IroomList)InternalroomList; } }
        private Ch5_Sample_Contract.RoomSelect.roomList InternalroomList { get; set; }

        #endregion

        #region Construction and Initialization

        private static readonly IDictionary<int, uint> SubsystemButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 2 }, { 1, 3 }, { 2, 4 }, { 3, 5 }, { 4, 6 }, { 5, 7 }, { 6, 8 }, { 7, 9 }, { 8, 10 }, { 9, 11 }, { 10, 12 }, { 11, 13 }, { 12, 14 }, 
            { 13, 15 }, { 14, 16 }, { 15, 17 }, { 16, 18 }, { 17, 19 }, { 18, 20 }, { 19, 21 }};
        private static readonly IDictionary<int, uint> VsrcButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 22 }, { 1, 23 }, { 2, 24 }, { 3, 25 }, { 4, 26 }, { 5, 27 }, { 6, 28 }, { 7, 29 }, { 8, 30 }, { 9, 31 }, { 10, 32 }, { 11, 33 }, 
            { 12, 34 }, { 13, 35 }, { 14, 36 }, { 15, 37 }, { 16, 38 }, { 17, 39 }, { 18, 40 }, { 19, 41 }};
        private static readonly IDictionary<int, uint> MusicSourceSelectSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 43 }, { 1, 44 }, { 2, 45 }, { 3, 46 }, { 4, 47 }, { 5, 48 }, { 6, 49 }, { 7, 50 }, { 8, 51 }, { 9, 52 }, { 10, 53 }, { 11, 54 }, 
            { 12, 55 }, { 13, 56 }, { 14, 57 }, { 15, 58 }, { 16, 59 }, { 17, 60 }, { 18, 61 }, { 19, 62 }};
        private static readonly IDictionary<int, uint> TabButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 64 }, { 1, 65 }, { 2, 66 }, { 3, 67 }, { 4, 68 }};
        private static readonly IDictionary<int, uint> FloorSelectSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 69 }, { 1, 70 }, { 2, 71 }, { 3, 72 }, { 4, 73 }, { 5, 74 }, { 6, 75 }, { 7, 76 }, { 8, 77 }, { 9, 78 }};
        private static readonly IDictionary<int, uint> WholeHouseZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 81 }, { 1, 82 }, { 2, 83 }, { 3, 84 }, { 4, 85 }, { 5, 86 }, { 6, 87 }, { 7, 88 }, { 8, 89 }, { 9, 90 }, { 10, 91 }, { 11, 92 }, 
            { 12, 93 }, { 13, 94 }, { 14, 95 }, { 15, 96 }, { 16, 97 }, { 17, 98 }, { 18, 99 }, { 19, 100 }, { 20, 101 }, { 21, 102 }, { 22, 103 }, 
            { 23, 104 }, { 24, 105 }, { 25, 106 }, { 26, 107 }, { 27, 108 }, { 28, 109 }, { 29, 110 }};
        private static readonly IDictionary<int, uint> WholeHouseSubsystemSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 112 }, { 1, 113 }, { 2, 114 }, { 3, 115 }, { 4, 116 }, { 5, 117 }, { 6, 118 }, { 7, 119 }, { 8, 120 }, { 9, 121 }, { 10, 122 }, 
            { 11, 123 }, { 12, 124 }, { 13, 125 }, { 14, 126 }};
        private static readonly IDictionary<int, uint> LightButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 128 }, { 1, 129 }, { 2, 130 }, { 3, 131 }, { 4, 132 }, { 5, 133 }, { 6, 134 }, { 7, 135 }, { 8, 136 }, { 9, 137 }, { 10, 138 }, 
            { 11, 139 }, { 12, 140 }, { 13, 141 }, { 14, 142 }, { 15, 143 }, { 16, 144 }, { 17, 145 }, { 18, 146 }, { 19, 147 }};
        private static readonly IDictionary<int, uint> SecurityZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 149 }, { 1, 150 }, { 2, 151 }, { 3, 152 }, { 4, 153 }, { 5, 154 }, { 6, 155 }, { 7, 156 }, { 8, 157 }, { 9, 158 }, { 10, 159 }, 
            { 11, 160 }, { 12, 161 }, { 13, 162 }, { 14, 163 }, { 15, 164 }, { 16, 165 }, { 17, 166 }, { 18, 167 }, { 19, 168 }, { 20, 169 }, { 21, 170 }, 
            { 22, 171 }, { 23, 172 }, { 24, 173 }, { 25, 174 }, { 26, 175 }, { 27, 176 }, { 28, 177 }, { 29, 178 }, { 30, 179 }, { 31, 180 }, { 32, 181 }, 
            { 33, 182 }, { 34, 183 }, { 35, 184 }, { 36, 185 }, { 37, 186 }, { 38, 187 }, { 39, 188 }, { 40, 189 }, { 41, 190 }, { 42, 191 }, { 43, 192 }, 
            { 44, 193 }, { 45, 194 }, { 46, 195 }, { 47, 196 }, { 48, 197 }, { 49, 198 }, { 50, 199 }, { 51, 200 }, { 52, 201 }, { 53, 202 }, { 54, 203 }, 
            { 55, 204 }, { 56, 205 }, { 57, 206 }, { 58, 207 }, { 59, 208 }, { 60, 209 }, { 61, 210 }, { 62, 211 }, { 63, 212 }, { 64, 213 }, { 65, 214 }, 
            { 66, 215 }, { 67, 216 }, { 68, 217 }, { 69, 218 }, { 70, 219 }, { 71, 220 }, { 72, 221 }, { 73, 222 }, { 74, 223 }, { 75, 224 }, { 76, 225 }, 
            { 77, 226 }, { 78, 227 }, { 79, 228 }, { 80, 229 }, { 81, 230 }, { 82, 231 }, { 83, 232 }, { 84, 233 }, { 85, 234 }, { 86, 235 }, { 87, 236 }, 
            { 88, 237 }, { 89, 238 }, { 90, 239 }, { 91, 240 }, { 92, 241 }, { 93, 242 }, { 94, 243 }, { 95, 244 }, { 96, 245 }, { 97, 246 }, { 98, 247 }, 
            { 99, 248 }, { 100, 249 }, { 101, 250 }, { 102, 251 }, { 103, 252 }, { 104, 253 }, { 105, 254 }, { 106, 255 }, { 107, 256 }, { 108, 257 }, 
            { 109, 258 }, { 110, 259 }, { 111, 260 }, { 112, 261 }, { 113, 262 }, { 114, 263 }, { 115, 264 }, { 116, 265 }, { 117, 266 }, { 118, 267 }, 
            { 119, 268 }, { 120, 269 }, { 121, 270 }, { 122, 271 }, { 123, 272 }, { 124, 273 }, { 125, 274 }, { 126, 275 }, { 127, 276 }, { 128, 277 }, 
            { 129, 278 }, { 130, 279 }, { 131, 280 }, { 132, 281 }, { 133, 282 }, { 134, 283 }, { 135, 284 }, { 136, 285 }, { 137, 286 }, { 138, 287 }, 
            { 139, 288 }, { 140, 289 }, { 141, 290 }, { 142, 291 }, { 143, 292 }, { 144, 293 }, { 145, 294 }, { 146, 295 }, { 147, 296 }, { 148, 297 }, 
            { 149, 298 }, { 150, 299 }, { 151, 300 }, { 152, 301 }, { 153, 302 }, { 154, 303 }, { 155, 304 }, { 156, 305 }, { 157, 306 }, { 158, 307 }, 
            { 159, 308 }, { 160, 309 }, { 161, 310 }, { 162, 311 }, { 163, 312 }, { 164, 313 }, { 165, 314 }, { 166, 315 }, { 167, 316 }, { 168, 317 }, 
            { 169, 318 }, { 170, 319 }, { 171, 320 }, { 172, 321 }, { 173, 322 }, { 174, 323 }, { 175, 324 }, { 176, 325 }, { 177, 326 }, { 178, 327 }, 
            { 179, 328 }, { 180, 329 }, { 181, 330 }, { 182, 331 }, { 183, 332 }, { 184, 333 }, { 185, 334 }, { 186, 335 }, { 187, 336 }, { 188, 337 }, 
            { 189, 338 }, { 190, 339 }, { 191, 340 }, { 192, 341 }, { 193, 342 }, { 194, 343 }, { 195, 344 }, { 196, 345 }, { 197, 346 }, { 198, 347 }, 
            { 199, 348 }};
        private static readonly IDictionary<int, uint> ShadeButtonsSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 350 }, { 1, 351 }, { 2, 352 }, { 3, 353 }, { 4, 354 }, { 5, 355 }, { 6, 356 }, { 7, 357 }, { 8, 358 }, { 9, 359 }, { 10, 360 }, 
            { 11, 361 }, { 12, 362 }, { 13, 363 }, { 14, 364 }, { 15, 365 }, { 16, 366 }, { 17, 367 }, { 18, 368 }, { 19, 369 }};
        private static readonly IDictionary<int, uint> HomeMusicZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 370 }, { 1, 371 }, { 2, 372 }, { 3, 373 }, { 4, 374 }, { 5, 375 }, { 6, 376 }, { 7, 377 }, { 8, 378 }, { 9, 379 }, { 10, 380 }, 
            { 11, 381 }, { 12, 382 }, { 13, 383 }, { 14, 384 }, { 15, 385 }, { 16, 386 }, { 17, 387 }, { 18, 388 }, { 19, 389 }, { 20, 390 }, { 21, 391 }, 
            { 22, 392 }, { 23, 393 }, { 24, 394 }, { 25, 395 }, { 26, 396 }, { 27, 397 }, { 28, 398 }, { 29, 399 }, { 30, 400 }, { 31, 401 }, { 32, 402 }, 
            { 33, 403 }, { 34, 404 }, { 35, 405 }, { 36, 406 }, { 37, 407 }, { 38, 408 }, { 39, 409 }, { 40, 410 }, { 41, 411 }, { 42, 412 }, { 43, 413 }, 
            { 44, 414 }, { 45, 415 }, { 46, 416 }, { 47, 417 }, { 48, 418 }, { 49, 419 }, { 50, 420 }, { 51, 421 }, { 52, 422 }, { 53, 423 }, { 54, 424 }, 
            { 55, 425 }, { 56, 426 }, { 57, 427 }, { 58, 428 }, { 59, 429 }, { 60, 430 }, { 61, 431 }, { 62, 432 }, { 63, 433 }, { 64, 434 }, { 65, 435 }, 
            { 66, 436 }, { 67, 437 }, { 68, 438 }, { 69, 439 }, { 70, 440 }, { 71, 441 }, { 72, 442 }, { 73, 443 }, { 74, 444 }, { 75, 445 }, { 76, 446 }, 
            { 77, 447 }, { 78, 448 }, { 79, 449 }, { 80, 450 }, { 81, 451 }, { 82, 452 }, { 83, 453 }, { 84, 454 }, { 85, 455 }, { 86, 456 }, { 87, 457 }, 
            { 88, 458 }, { 89, 459 }, { 90, 460 }, { 91, 461 }, { 92, 462 }, { 93, 463 }, { 94, 464 }, { 95, 465 }, { 96, 466 }, { 97, 467 }, { 98, 468 }, 
            { 99, 469 }};
        private static readonly IDictionary<int, uint> MusicRoomControlSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 473 }, { 1, 474 }, { 2, 475 }, { 3, 476 }, { 4, 477 }, { 5, 478 }, { 6, 479 }, { 7, 480 }, { 8, 481 }, { 9, 482 }, { 10, 483 }, 
            { 11, 484 }, { 12, 485 }, { 13, 486 }, { 14, 487 }, { 15, 488 }, { 16, 489 }, { 17, 490 }, { 18, 491 }, { 19, 492 }, { 20, 493 }, { 21, 494 }, 
            { 22, 495 }, { 23, 496 }, { 24, 497 }};
        private static readonly IDictionary<int, uint> RoomSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 498 }, { 1, 499 }, { 2, 500 }, { 3, 501 }, { 4, 502 }, { 5, 503 }, { 6, 504 }, { 7, 505 }, { 8, 506 }, { 9, 507 }, { 10, 508 }, 
            { 11, 509 }, { 12, 510 }, { 13, 511 }, { 14, 512 }, { 15, 513 }, { 16, 514 }, { 17, 515 }, { 18, 516 }, { 19, 517 }};

        public Contract()
            : this(new List<BasicTriListWithSmartObject>().ToArray())
        {
        }

        public Contract(BasicTriListWithSmartObject device)
            : this(new [] { device })
        {
        }

        public Contract(BasicTriListWithSmartObject[] devices)
        {
            if (devices == null)
                throw new ArgumentNullException("Devices is null");

            ComponentMediator = new ComponentMediator();

            InternalSubsystemList = new Ch5_Sample_Contract.Subsystem.SubsystemList(ComponentMediator, 1);
            InternalSubsystemButton = new Ch5_Sample_Contract.Subsystem.SubsystemButton[SubsystemButtonSmartObjectIdMappings.Count];
            for (int index = 0; index < SubsystemButtonSmartObjectIdMappings.Count; index++)
            {
                InternalSubsystemButton[index] = new Ch5_Sample_Contract.Subsystem.SubsystemButton(ComponentMediator, SubsystemButtonSmartObjectIdMappings[index]);
            }
            InternalvsrcButton = new Ch5_Sample_Contract.videoSources.videoSource[VsrcButtonSmartObjectIdMappings.Count];
            for (int index = 0; index < VsrcButtonSmartObjectIdMappings.Count; index++)
            {
                InternalvsrcButton[index] = new Ch5_Sample_Contract.videoSources.videoSource(ComponentMediator, VsrcButtonSmartObjectIdMappings[index]);
            }
            InternalvsrcList = new Ch5_Sample_Contract.videoSources.videoSourceList(ComponentMediator, 42);
            InternalmusicSourceSelect = new Ch5_Sample_Contract.musicSources.musicSource[MusicSourceSelectSmartObjectIdMappings.Count];
            for (int index = 0; index < MusicSourceSelectSmartObjectIdMappings.Count; index++)
            {
                InternalmusicSourceSelect[index] = new Ch5_Sample_Contract.musicSources.musicSource(ComponentMediator, MusicSourceSelectSmartObjectIdMappings[index]);
            }
            InternalmusicSourceList = new Ch5_Sample_Contract.musicSources.musicSourceList(ComponentMediator, 63);
            InternalTabButton = new Ch5_Sample_Contract.TabButton.TabButton[TabButtonSmartObjectIdMappings.Count];
            for (int index = 0; index < TabButtonSmartObjectIdMappings.Count; index++)
            {
                InternalTabButton[index] = new Ch5_Sample_Contract.TabButton.TabButton(ComponentMediator, TabButtonSmartObjectIdMappings[index]);
            }
            InternalFloorSelect = new Ch5_Sample_Contract.Floors.FloorSelect[FloorSelectSmartObjectIdMappings.Count];
            for (int index = 0; index < FloorSelectSmartObjectIdMappings.Count; index++)
            {
                InternalFloorSelect[index] = new Ch5_Sample_Contract.Floors.FloorSelect(ComponentMediator, FloorSelectSmartObjectIdMappings[index]);
            }
            InternalFloorList = new Ch5_Sample_Contract.Floors.FloorList(ComponentMediator, 79);
            InternalWholeHouseZoneList = new Ch5_Sample_Contract.WholeHouseZone.WholeHouseZoneList(ComponentMediator, 80);
            InternalWholeHouseZone = new Ch5_Sample_Contract.WholeHouseZone.WholeHouseZone[WholeHouseZoneSmartObjectIdMappings.Count];
            for (int index = 0; index < WholeHouseZoneSmartObjectIdMappings.Count; index++)
            {
                InternalWholeHouseZone[index] = new Ch5_Sample_Contract.WholeHouseZone.WholeHouseZone(ComponentMediator, WholeHouseZoneSmartObjectIdMappings[index]);
            }
            InternalWholeHouseSubsystemList = new Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystemList(ComponentMediator, 111);
            InternalWholeHouseSubsystem = new Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystem[WholeHouseSubsystemSmartObjectIdMappings.Count];
            for (int index = 0; index < WholeHouseSubsystemSmartObjectIdMappings.Count; index++)
            {
                InternalWholeHouseSubsystem[index] = new Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystem(ComponentMediator, WholeHouseSubsystemSmartObjectIdMappings[index]);
            }
            InternalLightButtonList = new Ch5_Sample_Contract.Lights.LightButtonList(ComponentMediator, 127);
            InternalLightButton = new Ch5_Sample_Contract.Lights.LightButton[LightButtonSmartObjectIdMappings.Count];
            for (int index = 0; index < LightButtonSmartObjectIdMappings.Count; index++)
            {
                InternalLightButton[index] = new Ch5_Sample_Contract.Lights.LightButton(ComponentMediator, LightButtonSmartObjectIdMappings[index]);
            }
            InternalNumberOfSecurityZones = new Ch5_Sample_Contract.SecurityBypassList.NumberOfSecurityZones(ComponentMediator, 148);
            InternalSecurityZone = new Ch5_Sample_Contract.SecurityBypassList.SecurityZone[SecurityZoneSmartObjectIdMappings.Count];
            for (int index = 0; index < SecurityZoneSmartObjectIdMappings.Count; index++)
            {
                InternalSecurityZone[index] = new Ch5_Sample_Contract.SecurityBypassList.SecurityZone(ComponentMediator, SecurityZoneSmartObjectIdMappings[index]);
            }
            InternalShadesList = new Ch5_Sample_Contract.Shades.ShadesList(ComponentMediator, 349);
            InternalShadeButtons = new Ch5_Sample_Contract.Shades.ShadeButtons[ShadeButtonsSmartObjectIdMappings.Count];
            for (int index = 0; index < ShadeButtonsSmartObjectIdMappings.Count; index++)
            {
                InternalShadeButtons[index] = new Ch5_Sample_Contract.Shades.ShadeButtons(ComponentMediator, ShadeButtonsSmartObjectIdMappings[index]);
            }
            InternalHomeMusicZone = new Ch5_Sample_Contract.HomePageMusicControl.HomeMusicZone[HomeMusicZoneSmartObjectIdMappings.Count];
            for (int index = 0; index < HomeMusicZoneSmartObjectIdMappings.Count; index++)
            {
                InternalHomeMusicZone[index] = new Ch5_Sample_Contract.HomePageMusicControl.HomeMusicZone(ComponentMediator, HomeMusicZoneSmartObjectIdMappings[index]);
            }
            InternalHomeNumberOfMusicZones = new Ch5_Sample_Contract.HomePageMusicControl.HomeNumberOfMusicZones(ComponentMediator, 470);
            InternalMediaPlayerObject = new Ch5_Sample_Contract.MediaPlayer.MediaPlayerObject(ComponentMediator, 471);
            InternalmusicNumberOfRooms = new Ch5_Sample_Contract.MusicControl.musicNumberOfRooms(ComponentMediator, 472);
            InternalMusicRoomControl = new Ch5_Sample_Contract.MusicControl.MusicRoomControl[MusicRoomControlSmartObjectIdMappings.Count];
            for (int index = 0; index < MusicRoomControlSmartObjectIdMappings.Count; index++)
            {
                InternalMusicRoomControl[index] = new Ch5_Sample_Contract.MusicControl.MusicRoomControl(ComponentMediator, MusicRoomControlSmartObjectIdMappings[index]);
            }
            InternalRoom = new Ch5_Sample_Contract.RoomSelect.Room[RoomSmartObjectIdMappings.Count];
            for (int index = 0; index < RoomSmartObjectIdMappings.Count; index++)
            {
                InternalRoom[index] = new Ch5_Sample_Contract.RoomSelect.Room(ComponentMediator, RoomSmartObjectIdMappings[index]);
            }
            InternalroomList = new Ch5_Sample_Contract.RoomSelect.roomList(ComponentMediator, 518);

            for (int index = 0; index < devices.Length; index++)
            {
                AddDevice(devices[index]);
            }
        }

        public static void ClearDictionaries()
        {
            SubsystemButtonSmartObjectIdMappings.Clear();
            VsrcButtonSmartObjectIdMappings.Clear();
            MusicSourceSelectSmartObjectIdMappings.Clear();
            TabButtonSmartObjectIdMappings.Clear();
            FloorSelectSmartObjectIdMappings.Clear();
            WholeHouseZoneSmartObjectIdMappings.Clear();
            WholeHouseSubsystemSmartObjectIdMappings.Clear();
            LightButtonSmartObjectIdMappings.Clear();
            SecurityZoneSmartObjectIdMappings.Clear();
            ShadeButtonsSmartObjectIdMappings.Clear();
            HomeMusicZoneSmartObjectIdMappings.Clear();
            MusicRoomControlSmartObjectIdMappings.Clear();
            RoomSmartObjectIdMappings.Clear();

        }

        #endregion

        #region Standard Contract Members

        public object UserObject { get; set; }

        public void AddDevice(BasicTriListWithSmartObject device)
        {
            InternalSubsystemList.AddDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalSubsystemButton[index].AddDevice(device);
            }
            for (int index = 0; index < 20; index++)
            {
                InternalvsrcButton[index].AddDevice(device);
            }
            InternalvsrcList.AddDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalmusicSourceSelect[index].AddDevice(device);
            }
            InternalmusicSourceList.AddDevice(device);
            for (int index = 0; index < 5; index++)
            {
                InternalTabButton[index].AddDevice(device);
            }
            for (int index = 0; index < 10; index++)
            {
                InternalFloorSelect[index].AddDevice(device);
            }
            InternalFloorList.AddDevice(device);
            InternalWholeHouseZoneList.AddDevice(device);
            for (int index = 0; index < 30; index++)
            {
                InternalWholeHouseZone[index].AddDevice(device);
            }
            InternalWholeHouseSubsystemList.AddDevice(device);
            for (int index = 0; index < 15; index++)
            {
                InternalWholeHouseSubsystem[index].AddDevice(device);
            }
            InternalLightButtonList.AddDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalLightButton[index].AddDevice(device);
            }
            InternalNumberOfSecurityZones.AddDevice(device);
            for (int index = 0; index < 200; index++)
            {
                InternalSecurityZone[index].AddDevice(device);
            }
            InternalShadesList.AddDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalShadeButtons[index].AddDevice(device);
            }
            for (int index = 0; index < 100; index++)
            {
                InternalHomeMusicZone[index].AddDevice(device);
            }
            InternalHomeNumberOfMusicZones.AddDevice(device);
            InternalMediaPlayerObject.AddDevice(device);
            InternalmusicNumberOfRooms.AddDevice(device);
            for (int index = 0; index < 25; index++)
            {
                InternalMusicRoomControl[index].AddDevice(device);
            }
            for (int index = 0; index < 20; index++)
            {
                InternalRoom[index].AddDevice(device);
            }
            InternalroomList.AddDevice(device);
        }

        public void RemoveDevice(BasicTriListWithSmartObject device)
        {
            InternalSubsystemList.RemoveDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalSubsystemButton[index].RemoveDevice(device);
            }
            for (int index = 0; index < 20; index++)
            {
                InternalvsrcButton[index].RemoveDevice(device);
            }
            InternalvsrcList.RemoveDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalmusicSourceSelect[index].RemoveDevice(device);
            }
            InternalmusicSourceList.RemoveDevice(device);
            for (int index = 0; index < 5; index++)
            {
                InternalTabButton[index].RemoveDevice(device);
            }
            for (int index = 0; index < 10; index++)
            {
                InternalFloorSelect[index].RemoveDevice(device);
            }
            InternalFloorList.RemoveDevice(device);
            InternalWholeHouseZoneList.RemoveDevice(device);
            for (int index = 0; index < 30; index++)
            {
                InternalWholeHouseZone[index].RemoveDevice(device);
            }
            InternalWholeHouseSubsystemList.RemoveDevice(device);
            for (int index = 0; index < 15; index++)
            {
                InternalWholeHouseSubsystem[index].RemoveDevice(device);
            }
            InternalLightButtonList.RemoveDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalLightButton[index].RemoveDevice(device);
            }
            InternalNumberOfSecurityZones.RemoveDevice(device);
            for (int index = 0; index < 200; index++)
            {
                InternalSecurityZone[index].RemoveDevice(device);
            }
            InternalShadesList.RemoveDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalShadeButtons[index].RemoveDevice(device);
            }
            for (int index = 0; index < 100; index++)
            {
                InternalHomeMusicZone[index].RemoveDevice(device);
            }
            InternalHomeNumberOfMusicZones.RemoveDevice(device);
            InternalMediaPlayerObject.RemoveDevice(device);
            InternalmusicNumberOfRooms.RemoveDevice(device);
            for (int index = 0; index < 25; index++)
            {
                InternalMusicRoomControl[index].RemoveDevice(device);
            }
            for (int index = 0; index < 20; index++)
            {
                InternalRoom[index].RemoveDevice(device);
            }
            InternalroomList.RemoveDevice(device);
        }

        #endregion

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            InternalSubsystemList.Dispose();
            for (int index = 0; index < 20; index++)
            {
                InternalSubsystemButton[index].Dispose();
            }
            for (int index = 0; index < 20; index++)
            {
                InternalvsrcButton[index].Dispose();
            }
            InternalvsrcList.Dispose();
            for (int index = 0; index < 20; index++)
            {
                InternalmusicSourceSelect[index].Dispose();
            }
            InternalmusicSourceList.Dispose();
            for (int index = 0; index < 5; index++)
            {
                InternalTabButton[index].Dispose();
            }
            for (int index = 0; index < 10; index++)
            {
                InternalFloorSelect[index].Dispose();
            }
            InternalFloorList.Dispose();
            InternalWholeHouseZoneList.Dispose();
            for (int index = 0; index < 30; index++)
            {
                InternalWholeHouseZone[index].Dispose();
            }
            InternalWholeHouseSubsystemList.Dispose();
            for (int index = 0; index < 15; index++)
            {
                InternalWholeHouseSubsystem[index].Dispose();
            }
            InternalLightButtonList.Dispose();
            for (int index = 0; index < 20; index++)
            {
                InternalLightButton[index].Dispose();
            }
            InternalNumberOfSecurityZones.Dispose();
            for (int index = 0; index < 200; index++)
            {
                InternalSecurityZone[index].Dispose();
            }
            InternalShadesList.Dispose();
            for (int index = 0; index < 20; index++)
            {
                InternalShadeButtons[index].Dispose();
            }
            for (int index = 0; index < 100; index++)
            {
                InternalHomeMusicZone[index].Dispose();
            }
            InternalHomeNumberOfMusicZones.Dispose();
            InternalMediaPlayerObject.Dispose();
            InternalmusicNumberOfRooms.Dispose();
            for (int index = 0; index < 25; index++)
            {
                InternalMusicRoomControl[index].Dispose();
            }
            for (int index = 0; index < 20; index++)
            {
                InternalRoom[index].Dispose();
            }
            InternalroomList.Dispose();
            ComponentMediator.Dispose(); 
        }

        #endregion

    }
}
