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

        public Ch5_Sample_Contract.RoomSelect.IroomList roomList { get { return (Ch5_Sample_Contract.RoomSelect.IroomList)InternalroomList; } }
        private Ch5_Sample_Contract.RoomSelect.roomList InternalroomList { get; set; }

        public Ch5_Sample_Contract.RoomSelect.IRoom[] roomButton { get { return InternalroomButton.Cast<Ch5_Sample_Contract.RoomSelect.IRoom>().ToArray(); } }
        private Ch5_Sample_Contract.RoomSelect.Room[] InternalroomButton { get; set; }

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

        #endregion

        #region Construction and Initialization

        private static readonly IDictionary<int, uint> SubsystemButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 2 }, { 1, 3 }, { 2, 4 }, { 3, 5 }, { 4, 6 }, { 5, 7 }, { 6, 8 }, { 7, 9 }, { 8, 10 }, { 9, 11 }, { 10, 12 }, { 11, 13 }, { 12, 14 }, 
            { 13, 15 }, { 14, 16 }, { 15, 17 }, { 16, 18 }, { 17, 19 }, { 18, 20 }, { 19, 21 }};
        private static readonly IDictionary<int, uint> VsrcButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 22 }, { 1, 23 }, { 2, 24 }, { 3, 25 }, { 4, 26 }, { 5, 27 }, { 6, 28 }, { 7, 29 }, { 8, 30 }, { 9, 31 }, { 10, 32 }, { 11, 33 }, 
            { 12, 34 }, { 13, 35 }, { 14, 36 }, { 15, 37 }, { 16, 38 }, { 17, 39 }, { 18, 40 }, { 19, 41 }};
        private static readonly IDictionary<int, uint> RoomButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 44 }, { 1, 45 }, { 2, 46 }, { 3, 47 }, { 4, 48 }, { 5, 49 }, { 6, 50 }, { 7, 51 }, { 8, 52 }, { 9, 53 }, { 10, 54 }, { 11, 55 }, 
            { 12, 56 }, { 13, 57 }, { 14, 58 }, { 15, 59 }, { 16, 60 }, { 17, 61 }, { 18, 62 }, { 19, 63 }};
        private static readonly IDictionary<int, uint> MusicSourceSelectSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 64 }, { 1, 65 }, { 2, 66 }, { 3, 67 }, { 4, 68 }, { 5, 69 }, { 6, 70 }, { 7, 71 }, { 8, 72 }, { 9, 73 }, { 10, 74 }, { 11, 75 }, 
            { 12, 76 }, { 13, 77 }, { 14, 78 }, { 15, 79 }, { 16, 80 }, { 17, 81 }, { 18, 82 }, { 19, 83 }};
        private static readonly IDictionary<int, uint> TabButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 85 }, { 1, 86 }, { 2, 87 }, { 3, 88 }, { 4, 89 }};
        private static readonly IDictionary<int, uint> FloorSelectSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 90 }, { 1, 91 }, { 2, 92 }, { 3, 93 }, { 4, 94 }, { 5, 95 }, { 6, 96 }, { 7, 97 }, { 8, 98 }, { 9, 99 }};
        private static readonly IDictionary<int, uint> WholeHouseZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 102 }, { 1, 103 }, { 2, 104 }, { 3, 105 }, { 4, 106 }, { 5, 107 }, { 6, 108 }, { 7, 109 }, { 8, 110 }, { 9, 111 }, { 10, 112 }, 
            { 11, 113 }, { 12, 114 }, { 13, 115 }, { 14, 116 }, { 15, 117 }, { 16, 118 }, { 17, 119 }, { 18, 120 }, { 19, 121 }, { 20, 122 }, { 21, 123 }, 
            { 22, 124 }, { 23, 125 }, { 24, 126 }, { 25, 127 }, { 26, 128 }, { 27, 129 }, { 28, 130 }, { 29, 131 }};
        private static readonly IDictionary<int, uint> WholeHouseSubsystemSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 133 }, { 1, 134 }, { 2, 135 }, { 3, 136 }, { 4, 137 }, { 5, 138 }, { 6, 139 }, { 7, 140 }, { 8, 141 }, { 9, 142 }, { 10, 143 }, 
            { 11, 144 }, { 12, 145 }, { 13, 146 }, { 14, 147 }};
        private static readonly IDictionary<int, uint> LightButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 149 }, { 1, 150 }, { 2, 151 }, { 3, 152 }, { 4, 153 }, { 5, 154 }, { 6, 155 }, { 7, 156 }, { 8, 157 }, { 9, 158 }, { 10, 159 }, 
            { 11, 160 }, { 12, 161 }, { 13, 162 }, { 14, 163 }, { 15, 164 }, { 16, 165 }, { 17, 166 }, { 18, 167 }, { 19, 168 }};
        private static readonly IDictionary<int, uint> SecurityZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 170 }, { 1, 171 }, { 2, 172 }, { 3, 173 }, { 4, 174 }, { 5, 175 }, { 6, 176 }, { 7, 177 }, { 8, 178 }, { 9, 179 }, { 10, 180 }, 
            { 11, 181 }, { 12, 182 }, { 13, 183 }, { 14, 184 }, { 15, 185 }, { 16, 186 }, { 17, 187 }, { 18, 188 }, { 19, 189 }, { 20, 190 }, { 21, 191 }, 
            { 22, 192 }, { 23, 193 }, { 24, 194 }, { 25, 195 }, { 26, 196 }, { 27, 197 }, { 28, 198 }, { 29, 199 }, { 30, 200 }, { 31, 201 }, { 32, 202 }, 
            { 33, 203 }, { 34, 204 }, { 35, 205 }, { 36, 206 }, { 37, 207 }, { 38, 208 }, { 39, 209 }, { 40, 210 }, { 41, 211 }, { 42, 212 }, { 43, 213 }, 
            { 44, 214 }, { 45, 215 }, { 46, 216 }, { 47, 217 }, { 48, 218 }, { 49, 219 }, { 50, 220 }, { 51, 221 }, { 52, 222 }, { 53, 223 }, { 54, 224 }, 
            { 55, 225 }, { 56, 226 }, { 57, 227 }, { 58, 228 }, { 59, 229 }, { 60, 230 }, { 61, 231 }, { 62, 232 }, { 63, 233 }, { 64, 234 }, { 65, 235 }, 
            { 66, 236 }, { 67, 237 }, { 68, 238 }, { 69, 239 }, { 70, 240 }, { 71, 241 }, { 72, 242 }, { 73, 243 }, { 74, 244 }, { 75, 245 }, { 76, 246 }, 
            { 77, 247 }, { 78, 248 }, { 79, 249 }, { 80, 250 }, { 81, 251 }, { 82, 252 }, { 83, 253 }, { 84, 254 }, { 85, 255 }, { 86, 256 }, { 87, 257 }, 
            { 88, 258 }, { 89, 259 }, { 90, 260 }, { 91, 261 }, { 92, 262 }, { 93, 263 }, { 94, 264 }, { 95, 265 }, { 96, 266 }, { 97, 267 }, { 98, 268 }, 
            { 99, 269 }, { 100, 270 }, { 101, 271 }, { 102, 272 }, { 103, 273 }, { 104, 274 }, { 105, 275 }, { 106, 276 }, { 107, 277 }, { 108, 278 }, 
            { 109, 279 }, { 110, 280 }, { 111, 281 }, { 112, 282 }, { 113, 283 }, { 114, 284 }, { 115, 285 }, { 116, 286 }, { 117, 287 }, { 118, 288 }, 
            { 119, 289 }, { 120, 290 }, { 121, 291 }, { 122, 292 }, { 123, 293 }, { 124, 294 }, { 125, 295 }, { 126, 296 }, { 127, 297 }, { 128, 298 }, 
            { 129, 299 }, { 130, 300 }, { 131, 301 }, { 132, 302 }, { 133, 303 }, { 134, 304 }, { 135, 305 }, { 136, 306 }, { 137, 307 }, { 138, 308 }, 
            { 139, 309 }, { 140, 310 }, { 141, 311 }, { 142, 312 }, { 143, 313 }, { 144, 314 }, { 145, 315 }, { 146, 316 }, { 147, 317 }, { 148, 318 }, 
            { 149, 319 }, { 150, 320 }, { 151, 321 }, { 152, 322 }, { 153, 323 }, { 154, 324 }, { 155, 325 }, { 156, 326 }, { 157, 327 }, { 158, 328 }, 
            { 159, 329 }, { 160, 330 }, { 161, 331 }, { 162, 332 }, { 163, 333 }, { 164, 334 }, { 165, 335 }, { 166, 336 }, { 167, 337 }, { 168, 338 }, 
            { 169, 339 }, { 170, 340 }, { 171, 341 }, { 172, 342 }, { 173, 343 }, { 174, 344 }, { 175, 345 }, { 176, 346 }, { 177, 347 }, { 178, 348 }, 
            { 179, 349 }, { 180, 350 }, { 181, 351 }, { 182, 352 }, { 183, 353 }, { 184, 354 }, { 185, 355 }, { 186, 356 }, { 187, 357 }, { 188, 358 }, 
            { 189, 359 }, { 190, 360 }, { 191, 361 }, { 192, 362 }, { 193, 363 }, { 194, 364 }, { 195, 365 }, { 196, 366 }, { 197, 367 }, { 198, 368 }, 
            { 199, 369 }};
        private static readonly IDictionary<int, uint> ShadeButtonsSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 371 }, { 1, 372 }, { 2, 373 }, { 3, 374 }, { 4, 375 }, { 5, 376 }, { 6, 377 }, { 7, 378 }, { 8, 379 }, { 9, 380 }, { 10, 381 }, 
            { 11, 382 }, { 12, 383 }, { 13, 384 }, { 14, 385 }, { 15, 386 }, { 16, 387 }, { 17, 388 }, { 18, 389 }, { 19, 390 }};
        private static readonly IDictionary<int, uint> HomeMusicZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 391 }, { 1, 392 }, { 2, 393 }, { 3, 394 }, { 4, 395 }, { 5, 396 }, { 6, 397 }, { 7, 398 }, { 8, 399 }, { 9, 400 }, { 10, 401 }, 
            { 11, 402 }, { 12, 403 }, { 13, 404 }, { 14, 405 }, { 15, 406 }, { 16, 407 }, { 17, 408 }, { 18, 409 }, { 19, 410 }, { 20, 411 }, { 21, 412 }, 
            { 22, 413 }, { 23, 414 }, { 24, 415 }, { 25, 416 }, { 26, 417 }, { 27, 418 }, { 28, 419 }, { 29, 420 }, { 30, 421 }, { 31, 422 }, { 32, 423 }, 
            { 33, 424 }, { 34, 425 }, { 35, 426 }, { 36, 427 }, { 37, 428 }, { 38, 429 }, { 39, 430 }, { 40, 431 }, { 41, 432 }, { 42, 433 }, { 43, 434 }, 
            { 44, 435 }, { 45, 436 }, { 46, 437 }, { 47, 438 }, { 48, 439 }, { 49, 440 }, { 50, 441 }, { 51, 442 }, { 52, 443 }, { 53, 444 }, { 54, 445 }, 
            { 55, 446 }, { 56, 447 }, { 57, 448 }, { 58, 449 }, { 59, 450 }, { 60, 451 }, { 61, 452 }, { 62, 453 }, { 63, 454 }, { 64, 455 }, { 65, 456 }, 
            { 66, 457 }, { 67, 458 }, { 68, 459 }, { 69, 460 }, { 70, 461 }, { 71, 462 }, { 72, 463 }, { 73, 464 }, { 74, 465 }, { 75, 466 }, { 76, 467 }, 
            { 77, 468 }, { 78, 469 }, { 79, 470 }, { 80, 471 }, { 81, 472 }, { 82, 473 }, { 83, 474 }, { 84, 475 }, { 85, 476 }, { 86, 477 }, { 87, 478 }, 
            { 88, 479 }, { 89, 480 }, { 90, 481 }, { 91, 482 }, { 92, 483 }, { 93, 484 }, { 94, 485 }, { 95, 486 }, { 96, 487 }, { 97, 488 }, { 98, 489 }, 
            { 99, 490 }};
        private static readonly IDictionary<int, uint> MusicRoomControlSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 494 }, { 1, 495 }, { 2, 496 }, { 3, 497 }, { 4, 498 }, { 5, 499 }, { 6, 500 }, { 7, 501 }, { 8, 502 }, { 9, 503 }, { 10, 504 }, 
            { 11, 505 }, { 12, 506 }, { 13, 507 }, { 14, 508 }, { 15, 509 }, { 16, 510 }, { 17, 511 }, { 18, 512 }, { 19, 513 }, { 20, 514 }, { 21, 515 }, 
            { 22, 516 }, { 23, 517 }, { 24, 518 }};

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
            InternalroomList = new Ch5_Sample_Contract.RoomSelect.roomList(ComponentMediator, 43);
            InternalroomButton = new Ch5_Sample_Contract.RoomSelect.Room[RoomButtonSmartObjectIdMappings.Count];
            for (int index = 0; index < RoomButtonSmartObjectIdMappings.Count; index++)
            {
                InternalroomButton[index] = new Ch5_Sample_Contract.RoomSelect.Room(ComponentMediator, RoomButtonSmartObjectIdMappings[index]);
            }
            InternalmusicSourceSelect = new Ch5_Sample_Contract.musicSources.musicSource[MusicSourceSelectSmartObjectIdMappings.Count];
            for (int index = 0; index < MusicSourceSelectSmartObjectIdMappings.Count; index++)
            {
                InternalmusicSourceSelect[index] = new Ch5_Sample_Contract.musicSources.musicSource(ComponentMediator, MusicSourceSelectSmartObjectIdMappings[index]);
            }
            InternalmusicSourceList = new Ch5_Sample_Contract.musicSources.musicSourceList(ComponentMediator, 84);
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
            InternalFloorList = new Ch5_Sample_Contract.Floors.FloorList(ComponentMediator, 100);
            InternalWholeHouseZoneList = new Ch5_Sample_Contract.WholeHouseZone.WholeHouseZoneList(ComponentMediator, 101);
            InternalWholeHouseZone = new Ch5_Sample_Contract.WholeHouseZone.WholeHouseZone[WholeHouseZoneSmartObjectIdMappings.Count];
            for (int index = 0; index < WholeHouseZoneSmartObjectIdMappings.Count; index++)
            {
                InternalWholeHouseZone[index] = new Ch5_Sample_Contract.WholeHouseZone.WholeHouseZone(ComponentMediator, WholeHouseZoneSmartObjectIdMappings[index]);
            }
            InternalWholeHouseSubsystemList = new Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystemList(ComponentMediator, 132);
            InternalWholeHouseSubsystem = new Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystem[WholeHouseSubsystemSmartObjectIdMappings.Count];
            for (int index = 0; index < WholeHouseSubsystemSmartObjectIdMappings.Count; index++)
            {
                InternalWholeHouseSubsystem[index] = new Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystem(ComponentMediator, WholeHouseSubsystemSmartObjectIdMappings[index]);
            }
            InternalLightButtonList = new Ch5_Sample_Contract.Lights.LightButtonList(ComponentMediator, 148);
            InternalLightButton = new Ch5_Sample_Contract.Lights.LightButton[LightButtonSmartObjectIdMappings.Count];
            for (int index = 0; index < LightButtonSmartObjectIdMappings.Count; index++)
            {
                InternalLightButton[index] = new Ch5_Sample_Contract.Lights.LightButton(ComponentMediator, LightButtonSmartObjectIdMappings[index]);
            }
            InternalNumberOfSecurityZones = new Ch5_Sample_Contract.SecurityBypassList.NumberOfSecurityZones(ComponentMediator, 169);
            InternalSecurityZone = new Ch5_Sample_Contract.SecurityBypassList.SecurityZone[SecurityZoneSmartObjectIdMappings.Count];
            for (int index = 0; index < SecurityZoneSmartObjectIdMappings.Count; index++)
            {
                InternalSecurityZone[index] = new Ch5_Sample_Contract.SecurityBypassList.SecurityZone(ComponentMediator, SecurityZoneSmartObjectIdMappings[index]);
            }
            InternalShadesList = new Ch5_Sample_Contract.Shades.ShadesList(ComponentMediator, 370);
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
            InternalHomeNumberOfMusicZones = new Ch5_Sample_Contract.HomePageMusicControl.HomeNumberOfMusicZones(ComponentMediator, 491);
            InternalMediaPlayerObject = new Ch5_Sample_Contract.MediaPlayer.MediaPlayerObject(ComponentMediator, 492);
            InternalmusicNumberOfRooms = new Ch5_Sample_Contract.MusicControl.musicNumberOfRooms(ComponentMediator, 493);
            InternalMusicRoomControl = new Ch5_Sample_Contract.MusicControl.MusicRoomControl[MusicRoomControlSmartObjectIdMappings.Count];
            for (int index = 0; index < MusicRoomControlSmartObjectIdMappings.Count; index++)
            {
                InternalMusicRoomControl[index] = new Ch5_Sample_Contract.MusicControl.MusicRoomControl(ComponentMediator, MusicRoomControlSmartObjectIdMappings[index]);
            }

            for (int index = 0; index < devices.Length; index++)
            {
                AddDevice(devices[index]);
            }
        }

        public static void ClearDictionaries()
        {
            SubsystemButtonSmartObjectIdMappings.Clear();
            VsrcButtonSmartObjectIdMappings.Clear();
            RoomButtonSmartObjectIdMappings.Clear();
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
            InternalroomList.AddDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalroomButton[index].AddDevice(device);
            }
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
            InternalroomList.RemoveDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalroomButton[index].RemoveDevice(device);
            }
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
            InternalroomList.Dispose();
            for (int index = 0; index < 20; index++)
            {
                InternalroomButton[index].Dispose();
            }
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
            ComponentMediator.Dispose(); 
        }

        #endregion

    }
}
