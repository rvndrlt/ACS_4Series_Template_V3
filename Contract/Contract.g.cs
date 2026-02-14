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
            { 0, 159 }, { 1, 160 }, { 2, 161 }, { 3, 162 }, { 4, 163 }, { 5, 164 }, { 6, 165 }, { 7, 166 }, { 8, 167 }, { 9, 168 }};
        private static readonly IDictionary<int, uint> LightButtonSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 170 }, { 1, 171 }, { 2, 172 }, { 3, 173 }, { 4, 174 }, { 5, 175 }, { 6, 176 }, { 7, 177 }, { 8, 178 }, { 9, 179 }, { 10, 180 }, 
            { 11, 181 }, { 12, 182 }, { 13, 183 }, { 14, 184 }, { 15, 185 }, { 16, 186 }, { 17, 187 }, { 18, 188 }, { 19, 189 }};
        private static readonly IDictionary<int, uint> SecurityZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 191 }, { 1, 192 }, { 2, 193 }, { 3, 194 }, { 4, 195 }, { 5, 196 }, { 6, 197 }, { 7, 198 }, { 8, 199 }, { 9, 200 }, { 10, 201 }, 
            { 11, 202 }, { 12, 203 }, { 13, 204 }, { 14, 205 }, { 15, 206 }, { 16, 207 }, { 17, 208 }, { 18, 209 }, { 19, 210 }, { 20, 211 }, { 21, 212 }, 
            { 22, 213 }, { 23, 214 }, { 24, 215 }, { 25, 216 }, { 26, 217 }, { 27, 218 }, { 28, 219 }, { 29, 220 }, { 30, 221 }, { 31, 222 }, { 32, 223 }, 
            { 33, 224 }, { 34, 225 }, { 35, 226 }, { 36, 227 }, { 37, 228 }, { 38, 229 }, { 39, 230 }, { 40, 231 }, { 41, 232 }, { 42, 233 }, { 43, 234 }, 
            { 44, 235 }, { 45, 236 }, { 46, 237 }, { 47, 238 }, { 48, 239 }, { 49, 240 }, { 50, 241 }, { 51, 242 }, { 52, 243 }, { 53, 244 }, { 54, 245 }, 
            { 55, 246 }, { 56, 247 }, { 57, 248 }, { 58, 249 }, { 59, 250 }, { 60, 251 }, { 61, 252 }, { 62, 253 }, { 63, 254 }, { 64, 255 }, { 65, 256 }, 
            { 66, 257 }, { 67, 258 }, { 68, 259 }, { 69, 260 }, { 70, 261 }, { 71, 262 }, { 72, 263 }, { 73, 264 }, { 74, 265 }, { 75, 266 }, { 76, 267 }, 
            { 77, 268 }, { 78, 269 }, { 79, 270 }, { 80, 271 }, { 81, 272 }, { 82, 273 }, { 83, 274 }, { 84, 275 }, { 85, 276 }, { 86, 277 }, { 87, 278 }, 
            { 88, 279 }, { 89, 280 }, { 90, 281 }, { 91, 282 }, { 92, 283 }, { 93, 284 }, { 94, 285 }, { 95, 286 }, { 96, 287 }, { 97, 288 }, { 98, 289 }, 
            { 99, 290 }, { 100, 291 }, { 101, 292 }, { 102, 293 }, { 103, 294 }, { 104, 295 }, { 105, 296 }, { 106, 297 }, { 107, 298 }, { 108, 299 }, 
            { 109, 300 }, { 110, 301 }, { 111, 302 }, { 112, 303 }, { 113, 304 }, { 114, 305 }, { 115, 306 }, { 116, 307 }, { 117, 308 }, { 118, 309 }, 
            { 119, 310 }, { 120, 311 }, { 121, 312 }, { 122, 313 }, { 123, 314 }, { 124, 315 }, { 125, 316 }, { 126, 317 }, { 127, 318 }, { 128, 319 }, 
            { 129, 320 }, { 130, 321 }, { 131, 322 }, { 132, 323 }, { 133, 324 }, { 134, 325 }, { 135, 326 }, { 136, 327 }, { 137, 328 }, { 138, 329 }, 
            { 139, 330 }, { 140, 331 }, { 141, 332 }, { 142, 333 }, { 143, 334 }, { 144, 335 }, { 145, 336 }, { 146, 337 }, { 147, 338 }, { 148, 339 }, 
            { 149, 340 }, { 150, 341 }, { 151, 342 }, { 152, 343 }, { 153, 344 }, { 154, 345 }, { 155, 346 }, { 156, 347 }, { 157, 348 }, { 158, 349 }, 
            { 159, 350 }, { 160, 351 }, { 161, 352 }, { 162, 353 }, { 163, 354 }, { 164, 355 }, { 165, 356 }, { 166, 357 }, { 167, 358 }, { 168, 359 }, 
            { 169, 360 }, { 170, 361 }, { 171, 362 }, { 172, 363 }, { 173, 364 }, { 174, 365 }, { 175, 366 }, { 176, 367 }, { 177, 368 }, { 178, 369 }, 
            { 179, 370 }, { 180, 371 }, { 181, 372 }, { 182, 373 }, { 183, 374 }, { 184, 375 }, { 185, 376 }, { 186, 377 }, { 187, 378 }, { 188, 379 }, 
            { 189, 380 }, { 190, 381 }, { 191, 382 }, { 192, 383 }, { 193, 384 }, { 194, 385 }, { 195, 386 }, { 196, 387 }, { 197, 388 }, { 198, 389 }, 
            { 199, 390 }};
        private static readonly IDictionary<int, uint> ShadeButtonsSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 392 }, { 1, 393 }, { 2, 394 }, { 3, 395 }, { 4, 396 }, { 5, 397 }, { 6, 398 }, { 7, 399 }, { 8, 400 }, { 9, 401 }, { 10, 402 }, 
            { 11, 403 }, { 12, 404 }, { 13, 405 }, { 14, 406 }, { 15, 407 }, { 16, 408 }, { 17, 409 }, { 18, 410 }, { 19, 411 }};
        private static readonly IDictionary<int, uint> HomeMusicZoneSmartObjectIdMappings = new Dictionary<int, uint>{
            { 0, 412 }, { 1, 413 }, { 2, 414 }, { 3, 415 }, { 4, 416 }, { 5, 417 }, { 6, 418 }, { 7, 419 }, { 8, 420 }, { 9, 421 }, { 10, 422 }, 
            { 11, 423 }, { 12, 424 }, { 13, 425 }, { 14, 426 }, { 15, 427 }, { 16, 428 }, { 17, 429 }, { 18, 430 }, { 19, 431 }, { 20, 432 }, { 21, 433 }, 
            { 22, 434 }, { 23, 435 }, { 24, 436 }, { 25, 437 }, { 26, 438 }, { 27, 439 }, { 28, 440 }, { 29, 441 }, { 30, 442 }, { 31, 443 }, { 32, 444 }, 
            { 33, 445 }, { 34, 446 }, { 35, 447 }, { 36, 448 }, { 37, 449 }, { 38, 450 }, { 39, 451 }, { 40, 452 }, { 41, 453 }, { 42, 454 }, { 43, 455 }, 
            { 44, 456 }, { 45, 457 }, { 46, 458 }, { 47, 459 }, { 48, 460 }, { 49, 461 }, { 50, 462 }, { 51, 463 }, { 52, 464 }, { 53, 465 }, { 54, 466 }, 
            { 55, 467 }, { 56, 468 }, { 57, 469 }, { 58, 470 }, { 59, 471 }, { 60, 472 }, { 61, 473 }, { 62, 474 }, { 63, 475 }, { 64, 476 }, { 65, 477 }, 
            { 66, 478 }, { 67, 479 }, { 68, 480 }, { 69, 481 }, { 70, 482 }, { 71, 483 }, { 72, 484 }, { 73, 485 }, { 74, 486 }, { 75, 487 }, { 76, 488 }, 
            { 77, 489 }, { 78, 490 }, { 79, 491 }, { 80, 492 }, { 81, 493 }, { 82, 494 }, { 83, 495 }, { 84, 496 }, { 85, 497 }, { 86, 498 }, { 87, 499 }, 
            { 88, 500 }, { 89, 501 }, { 90, 502 }, { 91, 503 }, { 92, 504 }, { 93, 505 }, { 94, 506 }, { 95, 507 }, { 96, 508 }, { 97, 509 }, { 98, 510 }, 
            { 99, 511 }};

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
            InternalLightButtonList = new Ch5_Sample_Contract.Lights.LightButtonList(ComponentMediator, 169);
            InternalLightButton = new Ch5_Sample_Contract.Lights.LightButton[LightButtonSmartObjectIdMappings.Count];
            for (int index = 0; index < LightButtonSmartObjectIdMappings.Count; index++)
            {
                InternalLightButton[index] = new Ch5_Sample_Contract.Lights.LightButton(ComponentMediator, LightButtonSmartObjectIdMappings[index]);
            }
            InternalNumberOfSecurityZones = new Ch5_Sample_Contract.SecurityBypassList.NumberOfSecurityZones(ComponentMediator, 190);
            InternalSecurityZone = new Ch5_Sample_Contract.SecurityBypassList.SecurityZone[SecurityZoneSmartObjectIdMappings.Count];
            for (int index = 0; index < SecurityZoneSmartObjectIdMappings.Count; index++)
            {
                InternalSecurityZone[index] = new Ch5_Sample_Contract.SecurityBypassList.SecurityZone(ComponentMediator, SecurityZoneSmartObjectIdMappings[index]);
            }
            InternalShadesList = new Ch5_Sample_Contract.Shades.ShadesList(ComponentMediator, 391);
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
            InternalHomeNumberOfMusicZones = new Ch5_Sample_Contract.HomePageMusicControl.HomeNumberOfMusicZones(ComponentMediator, 512);

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
            for (int index = 0; index < 10; index++)
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
            for (int index = 0; index < 10; index++)
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
            for (int index = 0; index < 10; index++)
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
            ComponentMediator.Dispose(); 
        }

        #endregion

    }
}
