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

        public Ch5_Sample_Contract.MusicControl.ImusicNumberOfRooms musicNumberOfRooms { get { return (Ch5_Sample_Contract.MusicControl.ImusicNumberOfRooms)InternalmusicNumberOfRooms; } }
        private Ch5_Sample_Contract.MusicControl.musicNumberOfRooms InternalmusicNumberOfRooms { get; set; }

        public Ch5_Sample_Contract.MusicControl.IMusicRoomControl[] MusicRoomControl { get { return InternalMusicRoomControl.Cast<Ch5_Sample_Contract.MusicControl.IMusicRoomControl>().ToArray(); } }
        private Ch5_Sample_Contract.MusicControl.MusicRoomControl[] InternalMusicRoomControl { get; set; }

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

        #endregion

        #region Construction and Initialization

        private static readonly IDictionary<int, uint> SubsystemButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 2 }, { 1, 3 }, { 2, 4 }, { 3, 5 }, { 4, 6 }, { 5, 7 }, { 6, 8 }, { 7, 9 }, { 8, 10 }, { 9, 11 }, { 10, 12 }, { 11, 13 }, { 12, 14 }, 
            { 13, 15 }, { 14, 16 }, { 15, 17 }, { 16, 18 }, { 17, 19 }, { 18, 20 }, { 19, 21 }};
        private static readonly IDictionary<int, uint> MusicRoomControlSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 23 }, { 1, 24 }, { 2, 25 }, { 3, 26 }, { 4, 27 }, { 5, 28 }, { 6, 29 }, { 7, 30 }, { 8, 31 }, { 9, 32 }, { 10, 33 }, { 11, 34 }, 
            { 12, 35 }, { 13, 36 }, { 14, 37 }, { 15, 38 }, { 16, 39 }, { 17, 40 }, { 18, 41 }, { 19, 42 }, { 20, 43 }, { 21, 44 }, { 22, 45 }, { 23, 46 }, 
            { 24, 47 }};
        private static readonly IDictionary<int, uint> VsrcButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 48 }, { 1, 49 }, { 2, 50 }, { 3, 51 }, { 4, 52 }, { 5, 53 }, { 6, 54 }, { 7, 55 }, { 8, 56 }, { 9, 57 }, { 10, 58 }, { 11, 59 }, 
            { 12, 60 }, { 13, 61 }, { 14, 62 }, { 15, 63 }, { 16, 64 }, { 17, 65 }, { 18, 66 }, { 19, 67 }};
        private static readonly IDictionary<int, uint> RoomButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 70 }, { 1, 71 }, { 2, 72 }, { 3, 73 }, { 4, 74 }, { 5, 75 }, { 6, 76 }, { 7, 77 }, { 8, 78 }, { 9, 79 }, { 10, 80 }, { 11, 81 }, 
            { 12, 82 }, { 13, 83 }, { 14, 84 }, { 15, 85 }, { 16, 86 }, { 17, 87 }, { 18, 88 }, { 19, 89 }};
        private static readonly IDictionary<int, uint> MusicSourceSelectSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 90 }, { 1, 91 }, { 2, 92 }, { 3, 93 }, { 4, 94 }, { 5, 95 }, { 6, 96 }, { 7, 97 }, { 8, 98 }, { 9, 99 }, { 10, 100 }, { 11, 101 }, 
            { 12, 102 }, { 13, 103 }, { 14, 104 }, { 15, 105 }, { 16, 106 }, { 17, 107 }, { 18, 108 }, { 19, 109 }};
        private static readonly IDictionary<int, uint> TabButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 111 }, { 1, 112 }, { 2, 113 }, { 3, 114 }, { 4, 115 }};
        private static readonly IDictionary<int, uint> FloorSelectSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 116 }, { 1, 117 }, { 2, 118 }, { 3, 119 }, { 4, 120 }, { 5, 121 }, { 6, 122 }, { 7, 123 }, { 8, 124 }, { 9, 125 }};
        private static readonly IDictionary<int, uint> WholeHouseZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 128 }, { 1, 129 }, { 2, 130 }, { 3, 131 }, { 4, 132 }, { 5, 133 }, { 6, 134 }, { 7, 135 }, { 8, 136 }, { 9, 137 }, { 10, 138 }, 
            { 11, 139 }, { 12, 140 }, { 13, 141 }, { 14, 142 }, { 15, 143 }, { 16, 144 }, { 17, 145 }, { 18, 146 }, { 19, 147 }, { 20, 148 }, { 21, 149 }, 
            { 22, 150 }, { 23, 151 }, { 24, 152 }, { 25, 153 }, { 26, 154 }, { 27, 155 }, { 28, 156 }, { 29, 157 }};
        private static readonly IDictionary<int, uint> WholeHouseSubsystemSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 159 }, { 1, 160 }, { 2, 161 }, { 3, 162 }, { 4, 163 }, { 5, 164 }, { 6, 165 }, { 7, 166 }, { 8, 167 }, { 9, 168 }, { 10, 169 }, 
            { 11, 170 }, { 12, 171 }, { 13, 172 }, { 14, 173 }};
        private static readonly IDictionary<int, uint> LightButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 175 }, { 1, 176 }, { 2, 177 }, { 3, 178 }, { 4, 179 }, { 5, 180 }, { 6, 181 }, { 7, 182 }, { 8, 183 }, { 9, 184 }, { 10, 185 }, 
            { 11, 186 }, { 12, 187 }, { 13, 188 }, { 14, 189 }, { 15, 190 }, { 16, 191 }, { 17, 192 }, { 18, 193 }, { 19, 194 }};
        private static readonly IDictionary<int, uint> SecurityZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 196 }, { 1, 197 }, { 2, 198 }, { 3, 199 }, { 4, 200 }, { 5, 201 }, { 6, 202 }, { 7, 203 }, { 8, 204 }, { 9, 205 }, { 10, 206 }, 
            { 11, 207 }, { 12, 208 }, { 13, 209 }, { 14, 210 }, { 15, 211 }, { 16, 212 }, { 17, 213 }, { 18, 214 }, { 19, 215 }, { 20, 216 }, { 21, 217 }, 
            { 22, 218 }, { 23, 219 }, { 24, 220 }, { 25, 221 }, { 26, 222 }, { 27, 223 }, { 28, 224 }, { 29, 225 }, { 30, 226 }, { 31, 227 }, { 32, 228 }, 
            { 33, 229 }, { 34, 230 }, { 35, 231 }, { 36, 232 }, { 37, 233 }, { 38, 234 }, { 39, 235 }, { 40, 236 }, { 41, 237 }, { 42, 238 }, { 43, 239 }, 
            { 44, 240 }, { 45, 241 }, { 46, 242 }, { 47, 243 }, { 48, 244 }, { 49, 245 }, { 50, 246 }, { 51, 247 }, { 52, 248 }, { 53, 249 }, { 54, 250 }, 
            { 55, 251 }, { 56, 252 }, { 57, 253 }, { 58, 254 }, { 59, 255 }, { 60, 256 }, { 61, 257 }, { 62, 258 }, { 63, 259 }, { 64, 260 }, { 65, 261 }, 
            { 66, 262 }, { 67, 263 }, { 68, 264 }, { 69, 265 }, { 70, 266 }, { 71, 267 }, { 72, 268 }, { 73, 269 }, { 74, 270 }, { 75, 271 }, { 76, 272 }, 
            { 77, 273 }, { 78, 274 }, { 79, 275 }, { 80, 276 }, { 81, 277 }, { 82, 278 }, { 83, 279 }, { 84, 280 }, { 85, 281 }, { 86, 282 }, { 87, 283 }, 
            { 88, 284 }, { 89, 285 }, { 90, 286 }, { 91, 287 }, { 92, 288 }, { 93, 289 }, { 94, 290 }, { 95, 291 }, { 96, 292 }, { 97, 293 }, { 98, 294 }, 
            { 99, 295 }, { 100, 296 }, { 101, 297 }, { 102, 298 }, { 103, 299 }, { 104, 300 }, { 105, 301 }, { 106, 302 }, { 107, 303 }, { 108, 304 }, 
            { 109, 305 }, { 110, 306 }, { 111, 307 }, { 112, 308 }, { 113, 309 }, { 114, 310 }, { 115, 311 }, { 116, 312 }, { 117, 313 }, { 118, 314 }, 
            { 119, 315 }, { 120, 316 }, { 121, 317 }, { 122, 318 }, { 123, 319 }, { 124, 320 }, { 125, 321 }, { 126, 322 }, { 127, 323 }, { 128, 324 }, 
            { 129, 325 }, { 130, 326 }, { 131, 327 }, { 132, 328 }, { 133, 329 }, { 134, 330 }, { 135, 331 }, { 136, 332 }, { 137, 333 }, { 138, 334 }, 
            { 139, 335 }, { 140, 336 }, { 141, 337 }, { 142, 338 }, { 143, 339 }, { 144, 340 }, { 145, 341 }, { 146, 342 }, { 147, 343 }, { 148, 344 }, 
            { 149, 345 }, { 150, 346 }, { 151, 347 }, { 152, 348 }, { 153, 349 }, { 154, 350 }, { 155, 351 }, { 156, 352 }, { 157, 353 }, { 158, 354 }, 
            { 159, 355 }, { 160, 356 }, { 161, 357 }, { 162, 358 }, { 163, 359 }, { 164, 360 }, { 165, 361 }, { 166, 362 }, { 167, 363 }, { 168, 364 }, 
            { 169, 365 }, { 170, 366 }, { 171, 367 }, { 172, 368 }, { 173, 369 }, { 174, 370 }, { 175, 371 }, { 176, 372 }, { 177, 373 }, { 178, 374 }, 
            { 179, 375 }, { 180, 376 }, { 181, 377 }, { 182, 378 }, { 183, 379 }, { 184, 380 }, { 185, 381 }, { 186, 382 }, { 187, 383 }, { 188, 384 }, 
            { 189, 385 }, { 190, 386 }, { 191, 387 }, { 192, 388 }, { 193, 389 }, { 194, 390 }, { 195, 391 }, { 196, 392 }, { 197, 393 }, { 198, 394 }, 
            { 199, 395 }};
        private static readonly IDictionary<int, uint> ShadeButtonsSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 397 }, { 1, 398 }, { 2, 399 }, { 3, 400 }, { 4, 401 }, { 5, 402 }, { 6, 403 }, { 7, 404 }, { 8, 405 }, { 9, 406 }, { 10, 407 }, 
            { 11, 408 }, { 12, 409 }, { 13, 410 }, { 14, 411 }, { 15, 412 }, { 16, 413 }, { 17, 414 }, { 18, 415 }, { 19, 416 }};
        private static readonly IDictionary<int, uint> HomeMusicZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 417 }, { 1, 418 }, { 2, 419 }, { 3, 420 }, { 4, 421 }, { 5, 422 }, { 6, 423 }, { 7, 424 }, { 8, 425 }, { 9, 426 }, { 10, 427 }, 
            { 11, 428 }, { 12, 429 }, { 13, 430 }, { 14, 431 }, { 15, 432 }, { 16, 433 }, { 17, 434 }, { 18, 435 }, { 19, 436 }, { 20, 437 }, { 21, 438 }, 
            { 22, 439 }, { 23, 440 }, { 24, 441 }, { 25, 442 }, { 26, 443 }, { 27, 444 }, { 28, 445 }, { 29, 446 }, { 30, 447 }, { 31, 448 }, { 32, 449 }, 
            { 33, 450 }, { 34, 451 }, { 35, 452 }, { 36, 453 }, { 37, 454 }, { 38, 455 }, { 39, 456 }, { 40, 457 }, { 41, 458 }, { 42, 459 }, { 43, 460 }, 
            { 44, 461 }, { 45, 462 }, { 46, 463 }, { 47, 464 }, { 48, 465 }, { 49, 466 }, { 50, 467 }, { 51, 468 }, { 52, 469 }, { 53, 470 }, { 54, 471 }, 
            { 55, 472 }, { 56, 473 }, { 57, 474 }, { 58, 475 }, { 59, 476 }, { 60, 477 }, { 61, 478 }, { 62, 479 }, { 63, 480 }, { 64, 481 }, { 65, 482 }, 
            { 66, 483 }, { 67, 484 }, { 68, 485 }, { 69, 486 }, { 70, 487 }, { 71, 488 }, { 72, 489 }, { 73, 490 }, { 74, 491 }, { 75, 492 }, { 76, 493 }, 
            { 77, 494 }, { 78, 495 }, { 79, 496 }, { 80, 497 }, { 81, 498 }, { 82, 499 }, { 83, 500 }, { 84, 501 }, { 85, 502 }, { 86, 503 }, { 87, 504 }, 
            { 88, 505 }, { 89, 506 }, { 90, 507 }, { 91, 508 }, { 92, 509 }, { 93, 510 }, { 94, 511 }, { 95, 512 }, { 96, 513 }, { 97, 514 }, { 98, 515 }, 
            { 99, 516 }};

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
            InternalmusicNumberOfRooms = new Ch5_Sample_Contract.MusicControl.musicNumberOfRooms(ComponentMediator, 22);
            InternalMusicRoomControl = new Ch5_Sample_Contract.MusicControl.MusicRoomControl[MusicRoomControlSmartObjectIdMappings.Count];
            for (int index = 0; index < MusicRoomControlSmartObjectIdMappings.Count; index++)
            {
                InternalMusicRoomControl[index] = new Ch5_Sample_Contract.MusicControl.MusicRoomControl(ComponentMediator, MusicRoomControlSmartObjectIdMappings[index]);
            }
            InternalvsrcButton = new Ch5_Sample_Contract.videoSources.videoSource[VsrcButtonSmartObjectIdMappings.Count];
            for (int index = 0; index < VsrcButtonSmartObjectIdMappings.Count; index++)
            {
                InternalvsrcButton[index] = new Ch5_Sample_Contract.videoSources.videoSource(ComponentMediator, VsrcButtonSmartObjectIdMappings[index]);
            }
            InternalvsrcList = new Ch5_Sample_Contract.videoSources.videoSourceList(ComponentMediator, 68);
            InternalroomList = new Ch5_Sample_Contract.RoomSelect.roomList(ComponentMediator, 69);
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
            InternalmusicSourceList = new Ch5_Sample_Contract.musicSources.musicSourceList(ComponentMediator, 110);
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
            InternalFloorList = new Ch5_Sample_Contract.Floors.FloorList(ComponentMediator, 126);
            InternalWholeHouseZoneList = new Ch5_Sample_Contract.WholeHouseZone.WholeHouseZoneList(ComponentMediator, 127);
            InternalWholeHouseZone = new Ch5_Sample_Contract.WholeHouseZone.WholeHouseZone[WholeHouseZoneSmartObjectIdMappings.Count];
            for (int index = 0; index < WholeHouseZoneSmartObjectIdMappings.Count; index++)
            {
                InternalWholeHouseZone[index] = new Ch5_Sample_Contract.WholeHouseZone.WholeHouseZone(ComponentMediator, WholeHouseZoneSmartObjectIdMappings[index]);
            }
            InternalWholeHouseSubsystemList = new Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystemList(ComponentMediator, 158);
            InternalWholeHouseSubsystem = new Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystem[WholeHouseSubsystemSmartObjectIdMappings.Count];
            for (int index = 0; index < WholeHouseSubsystemSmartObjectIdMappings.Count; index++)
            {
                InternalWholeHouseSubsystem[index] = new Ch5_Sample_Contract.WholeHouseSubsystem.WholeHouseSubsystem(ComponentMediator, WholeHouseSubsystemSmartObjectIdMappings[index]);
            }
            InternalLightButtonList = new Ch5_Sample_Contract.Lights.LightButtonList(ComponentMediator, 174);
            InternalLightButton = new Ch5_Sample_Contract.Lights.LightButton[LightButtonSmartObjectIdMappings.Count];
            for (int index = 0; index < LightButtonSmartObjectIdMappings.Count; index++)
            {
                InternalLightButton[index] = new Ch5_Sample_Contract.Lights.LightButton(ComponentMediator, LightButtonSmartObjectIdMappings[index]);
            }
            InternalNumberOfSecurityZones = new Ch5_Sample_Contract.SecurityBypassList.NumberOfSecurityZones(ComponentMediator, 195);
            InternalSecurityZone = new Ch5_Sample_Contract.SecurityBypassList.SecurityZone[SecurityZoneSmartObjectIdMappings.Count];
            for (int index = 0; index < SecurityZoneSmartObjectIdMappings.Count; index++)
            {
                InternalSecurityZone[index] = new Ch5_Sample_Contract.SecurityBypassList.SecurityZone(ComponentMediator, SecurityZoneSmartObjectIdMappings[index]);
            }
            InternalShadesList = new Ch5_Sample_Contract.Shades.ShadesList(ComponentMediator, 396);
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
            InternalHomeNumberOfMusicZones = new Ch5_Sample_Contract.HomePageMusicControl.HomeNumberOfMusicZones(ComponentMediator, 517);
            InternalMediaPlayerObject = new Ch5_Sample_Contract.MediaPlayer.MediaPlayerObject(ComponentMediator, 518);

            for (int index = 0; index < devices.Length; index++)
            {
                AddDevice(devices[index]);
            }
        }

        public static void ClearDictionaries()
        {
            SubsystemButtonSmartObjectIdMappings.Clear();
            MusicRoomControlSmartObjectIdMappings.Clear();
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
            InternalmusicNumberOfRooms.AddDevice(device);
            for (int index = 0; index < 25; index++)
            {
                InternalMusicRoomControl[index].AddDevice(device);
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
        }

        public void RemoveDevice(BasicTriListWithSmartObject device)
        {
            InternalSubsystemList.RemoveDevice(device);
            for (int index = 0; index < 20; index++)
            {
                InternalSubsystemButton[index].RemoveDevice(device);
            }
            InternalmusicNumberOfRooms.RemoveDevice(device);
            for (int index = 0; index < 25; index++)
            {
                InternalMusicRoomControl[index].RemoveDevice(device);
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
            InternalmusicNumberOfRooms.Dispose();
            for (int index = 0; index < 25; index++)
            {
                InternalMusicRoomControl[index].Dispose();
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
            ComponentMediator.Dispose(); 
        }

        #endregion

    }
}
