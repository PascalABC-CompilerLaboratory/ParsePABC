type B = class
  yy: real;
end;

type A = class(B)
  xx: integer;
	function pp(test_field: integer): integer;
	begin
    		//var x := self.test_field;
  		test_field += 1;
	end;
end;

begin
end.